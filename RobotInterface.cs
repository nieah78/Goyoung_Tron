using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using System.IO.Ports;
using UnityEngine.UI;
using TMPro;

public enum GameState { // 게임 페이즈 구조 변경
    Waiting,
    Countdown,
    InGame,
    EndGame
}

public class RobotInterface : MonoBehaviour
{
    private GameState currentState;
    private Queue<string> dataQueue = new Queue<string>();

    const float MAX_HP = 100;
    const int GENERAL_DAMAGE = 18;

    private SerialPort portA, portB, portC;  // 각각 JoyA, JoyB, Compressor에 대응
    private  const int maxRetryCount = 100; // 재시도 횟수
    private int retryCount = 1;
    private Thread serialThread;
    private bool isRunning;
    private bool A_flag;
    private bool B_flag;
    private bool C_flag;

    // 시리얼 통신 데이터 저장 변수
    private string dataA;
    private string dataB;
    private string dataC;

    public TextMeshProUGUI errorText;
    
    struct Phase
    {
        public int step;
        public double countdownTimer;
        public int showCountdownTimer;
    }
    struct PlayerStatus
    {
        public float hp;
        public int bullet;

        //public int lastDamagedIndex;
        public float damagedTimer;

        public float shotDelay;
        public byte shotCommand;

        public float depletedSoundDelay;
        public bool winner;

        public float hitEffectTimer;

        public bool Hit;
        public bool Shoot;
    }


    PlayerStatus playerA, playerB;

    Phase phase;
    GameObject countdownTimer, countdownTimerGameGoing;
    Text countdownTimerText, countdownTimerGameGoingText;

    
    GameObject hpSlider1, hpSlider2, shieldSlider1, shieldSlider2, agiSlider1, agiSlider2, bulletSlider1, bulletSlider2;
    Image hpBarImage1, hpBarImage2;
    Color hpBarColor, hpBarColorHit;
    GameObject playerOneLogo, playerTwoLogo, winImage, drawImage, nameAxia, nameRic;

    GameObject waitingUi, gameCountdownUi, inGameUi, endUi;


    AudioSource audioCursor, audioIncrease, audioDecrease, audioStartup, audioCount, audioDeny, audioTransition, audioHit, audioDepleted,
        bgmBattle, bgmWait, bgmStat, bgmEnd;                            
    bool audioCursorFlag, audioIncreaseFlag, audioDecreaseFlag, audioStartupFlag, audioCountFlag, audioHitFlag, audioDepletedFlag,
        audioDenyFlag, audioTransitionFlag, bgmBattlePlayFlag, bgmWaitPlayFlag, bgmStatPlayFlag, bgmEndPlayFlag,
        bgmBattleStopFlag, bgmWaitStopFlag, bgmStatStopFlag, bgmEndStopFlag;
    
    float bgmWaitTimer = 0;
    int countCheck = 0;

    // 포트에서 데이터 읽기 함수 (스레드에서 실행)
    private void ReadSerialPorts()
    {
        while (isRunning)
        {
            lock (dataQueue) // 데이터 추가 시 thread-safe하게 관리
            {
                if (portA != null && portA.IsOpen)
                {
                    try
                    {
                        if (portA.BytesToRead > 0)
                        {
                            dataA = portA.ReadLine();  // 데이터를 읽어 변수에 저장
                            dataQueue.Enqueue(dataA);   // 큐에 데이터 추가
                            Console.WriteLine("JoyA로부터 받은 데이터: " + dataA);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("JoyA 포트 오류: " + ex.Message);
                    }
                }

                if (portB != null && portB.IsOpen)
                {
                    try
                    {
                        if (portB.BytesToRead > 0)
                        {
                            dataB = portB.ReadLine();
                            dataQueue.Enqueue(dataB);
                            Console.WriteLine("JoyB로부터 받은 데이터: " + dataB);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("JoyB 포트 오류: " + ex.Message);
                    }
                }

                if (portC != null && portC.IsOpen)
                {
                    try
                    {
                        if (portC.BytesToRead > 0)
                        {
                            dataC = portC.ReadLine();
                            dataQueue.Enqueue(dataC);
                            Console.WriteLine("Compressor로부터 받은 데이터: " + dataC);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Compressor 포트 오류: " + ex.Message);
                    }
                }
            }

            Thread.Sleep(50);  // 100ms 간격으로 반복
        }
    }



    void PortTest1(){
        string[] portNames = SerialPort.GetPortNames();

        if (portNames.Length > 0){        
            foreach (string portName in portNames){
                try{
                    SerialPort testPort = new SerialPort(portName, 115200);
                    testPort.ReadTimeout = 2000; // 타임아웃 설정 (ms)
                    testPort.Open();
                    
                    string response =  testPort.ReadLine();
                    if (response.Contains("TronA")){
                        portA = testPort;
                        testPort.WriteLine("AOK");
                        Debug.Log("TronA 연결");
                    } else if (response.Contains("TronB")){
                        portB = testPort;
                        testPort.WriteLine("BOK");
                        Debug.Log("TronB 연결");
                    } else if (response.Contains("Compressor")){
                        portC = testPort;
                        testPort.WriteLine("COK");
                        Debug.Log("Compressor 연결");
                    } else {
                        Debug.Log(portName + ": 일치하는 포트가 없습니다.");
                    }
                    Debug.Log("아두이노 연결 확인: " + portName);
                    
                }
                catch (TimeoutException){
                    Debug.Log("응답 시간 초과: " + portName);
                }
                catch(Exception e){
                    Debug.LogError("포트 " + portName + " 연결 실패: " + e.Message);
                }
            }
        }
    }
    
    public void ShowErrorMessage()
    {
        string errorMessages = "";
        
        if(portA == null) errorMessages += "Error: Fail to connect port TronA.\n";
        if(portB == null) errorMessages += "Error: Fail to connect port TronB.\n";
        if(portC == null) errorMessages += "Error: Fail to connect port Compressor.\n";

        if (!string.IsNullOrEmpty(errorMessages)){
            errorText.text = errorMessages;
            errorText.gameObject.SetActive(true); // 텍스트 활성화
        }
    }

    private IEnumerator TryConnectPorts(){ // 코루틴 함수
        while (retryCount < maxRetryCount){
            Debug.Log(retryCount + "번째 포트 연결 시도입니다.");
            PortTest1();
            ShowErrorMessage();

            if (portA != null && portB != null && portC != null){ // 모든 포트가 정상 연결되었을 때
                errorText.gameObject.SetActive(false);
                SetState(GameState.Waiting);
                yield break;
            }

            retryCount++;
            yield return new WaitForSeconds(5);    // 포트 재시도 간격 30s
        }
        // 최대 재시도 횟수 초과 시 컴퓨터 재부팅
        RestartComputer();
    }

    // Start is called before the first frame update
    void Start()
    {
        // 초기 상태에서 모든 UI를 비활성화합니다.
        errorText = GameObject.Find("ErrorText").GetComponent<TextMeshProUGUI>();
        waitingUi = GameObject.Find("WaitingUi");
        gameCountdownUi = GameObject.Find("GameCountdownUi");
        inGameUi = GameObject.Find("InGameUi");
        endUi = GameObject.Find("EndUi");
        countdownTimer = GameObject.Find("All-CountdownTimer");


        Debug.Log("RobotInterface preparing");
        phase = new Phase();
        phase.countdownTimer = 99;

        countdownTimerGameGoing = GameObject.Find("GameCountdown-CountdownTimer");
        countdownTimerText = countdownTimer.GetComponent<Text>();
        countdownTimerGameGoingText = countdownTimerGameGoing.GetComponent<Text>();


        hpSlider1 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerOne/hpGauge");
        agiSlider1 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerOne/agiGauge");
        shieldSlider1 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerOne/shieldGauge");
        bulletSlider1 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerOne/bulletGauge");
        hpSlider2 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerTwo/hpGauge");
        agiSlider2 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerTwo/agiGauge");
        shieldSlider2 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerTwo/shieldGauge");
        bulletSlider2 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerTwo/bulletGauge");
        
        hpBarImage1 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerOne/hpGauge/fill").GetComponent<Image>();
        hpBarImage2 = GameObject.Find("/GUI/Canvas/InGameUi/PlayerTwo/hpGauge/fill").GetComponent<Image>();
        hpBarColor = new Color(hpBarImage1.color.r, hpBarImage1.color.g, hpBarImage1.color.b, 1f);
        hpBarColorHit = new Color(0.95f, 0.45f, 0.2f, 1f);

        playerOneLogo = GameObject.Find("Logo-PlayerOne");
        playerTwoLogo = GameObject.Find("Logo-PlayerTwo");
        winImage = GameObject.Find("Win");
        drawImage = GameObject.Find("Draw");
        nameAxia = GameObject.Find("NameAxia");
        nameRic = GameObject.Find("NameRic");

        audioCursor = GameObject.Find("sounds/cursor").GetComponent<AudioSource>();
        audioIncrease = GameObject.Find("sounds/increase").GetComponent<AudioSource>();
        audioDecrease = GameObject.Find("sounds/decrease").GetComponent<AudioSource>();
        audioStartup = GameObject.Find("sounds/startup").GetComponent<AudioSource>();
        audioCount = GameObject.Find("sounds/count").GetComponent<AudioSource>();
        audioDeny = GameObject.Find("sounds/deny").GetComponent<AudioSource>();
        audioTransition = GameObject.Find("sounds/transition").GetComponent<AudioSource>();
        audioHit = GameObject.Find("sounds/hit").GetComponent<AudioSource>();
        audioDepleted = GameObject.Find("sounds/depleted").GetComponent<AudioSource>();
        bgmBattle = GameObject.Find("sounds/bgm-battle").GetComponent<AudioSource>();
        bgmWait = GameObject.Find("sounds/bgm-wait").GetComponent<AudioSource>();
        bgmStat = GameObject.Find("sounds/bgm-stat").GetComponent<AudioSource>();
        bgmEnd = GameObject.Find("sounds/bgm-end").GetComponent<AudioSource>();

        bgmWaitPlayFlag = true;
        errorText.gameObject.SetActive(false);
        waitingUi.SetActive(false);
        gameCountdownUi.SetActive(false);
        inGameUi.SetActive(false);
        endUi.SetActive(false);
        countdownTimer.SetActive(false);


        StartCoroutine(TryConnectPorts());


        isRunning = true;
        A_flag = true;
        B_flag = true;
        serialThread = new Thread(ReadSerialPorts);
        serialThread.Start();
    }


    private void RestartComputer(){
        Debug.Log("최대 재시도 횟수를 초과하여 컴퓨터 재부팅을 진행합니다.");
        System.Diagnostics.Process.Start("shutdown.exe", "/r /t 0");
    }

    // 포트 열기 함수
    private void OpenPorts() {
        try{
            if (!portA.IsOpen) {
                portA.Open();
                portA.WriteLine("AOK");
            }
            if (!portB.IsOpen) {
                portB.Open();
                portB.WriteLine("BOK");
            }

            if (!portC.IsOpen) {
                portC.Open();
                portC.WriteLine("COK");
            }
            Console.WriteLine("모든 포트가 열렸습니다.");
        }
        catch (Exception ex){
            Console.WriteLine("포트를 여는 도중 오류 발생: " + ex.Message);
        }
    }

    // 반복작업용 오디오 함수
    void PlayAudio(ref bool flag, AudioSource audioSource){
        if (flag){
            audioSource.Play();
            flag = false;
        }
    }

    void StopAudio(ref bool flag, AudioSource audioSource){
        if (flag){
            audioSource.Stop();
            flag = false;
        }
    }

    // Update is called once per frame
    void Update(){

        if (portA == null || portB == null || portC == null) {
            return; // 포트가 연결될 때까지 Update 함수 중단
        }

        lock (dataQueue){
            while (dataQueue.Count > 0){
                string data = dataQueue.Dequeue();
                ProcessData(data);  // 데이터를 처리하는 함수
            }
        }
    
        PlayAudio(ref audioStartupFlag, audioStartup);
        PlayAudio(ref audioCountFlag, audioCount);
        PlayAudio(ref audioDecreaseFlag, audioDecrease);
        PlayAudio(ref audioIncreaseFlag, audioIncrease);
        PlayAudio(ref audioCursorFlag, audioCursor);
        PlayAudio(ref audioDenyFlag, audioDeny);
        PlayAudio(ref audioTransitionFlag, audioTransition);
        PlayAudio(ref audioHitFlag, audioHit);
        PlayAudio(ref audioDepletedFlag, audioDepleted);

        PlayAudio(ref bgmBattlePlayFlag, bgmBattle);
        PlayAudio(ref bgmWaitPlayFlag, bgmWait);
        bgmWaitTimer = bgmWaitPlayFlag ? 0 : bgmWaitTimer; // 플래그에 따라 타이머 초기화

        PlayAudio(ref bgmStatPlayFlag, bgmStat);
        PlayAudio(ref bgmEndPlayFlag, bgmEnd);

        StopAudio(ref bgmBattleStopFlag, bgmBattle);
        StopAudio(ref bgmWaitStopFlag, bgmWait);
        bgmWaitTimer = bgmWaitStopFlag ? 0 : bgmWaitTimer; // 타이머 초기화
        StopAudio(ref bgmStatStopFlag, bgmStat);
        StopAudio(ref bgmEndStopFlag, bgmEnd);
        

        if (phase.countdownTimer > 0.0) {
            phase.countdownTimer -= Time.deltaTime;
            if (phase.countdownTimer < 0.0) phase.countdownTimer = 0.0;
            phase.showCountdownTimer = (int)Math.Ceiling(phase.countdownTimer);
            countdownTimerGameGoingText.text = countdownTimerText.text = phase.showCountdownTimer.ToString();
        }

        if(playerA.damagedTimer > 0.0) {
            playerA.damagedTimer -= Time.deltaTime;
        }
        if(playerB.damagedTimer > 0.0) {
            playerB.damagedTimer -= Time.deltaTime;
        }
        if(playerA.shotDelay > 0.0) {
            playerA.shotDelay -= Time.deltaTime;
        }
        if(playerB.shotDelay > 0.0) {
            playerB.shotDelay -= Time.deltaTime;
        }
        if(playerA.depletedSoundDelay > 0.0) {
            playerA.depletedSoundDelay -= Time.deltaTime;
        }
        if(playerB.depletedSoundDelay > 0.0) {
            playerB.depletedSoundDelay -= Time.deltaTime;
        }

        if(true) {
            if(playerA.hitEffectTimer > 0.0) {
                playerA.hitEffectTimer -= Time.deltaTime * 12f;
                if((int)playerA.hitEffectTimer % 2 == 0) {
                    hpBarImage1.color = hpBarColorHit;
                } else {
                    hpBarImage1.color = hpBarColor;
                }
                playerA.Hit = false;
            } else {

                hpBarImage1.color = hpBarColor;
            }

            if(playerB.hitEffectTimer > 0.0) {
                playerB.hitEffectTimer -= Time.deltaTime * 12f;
                if((int)playerB.hitEffectTimer % 2 == 0) {
                    hpBarImage2.color = hpBarColorHit;
                } else {
                    hpBarImage2.color = hpBarColor;
                }
                playerB.Hit = false; 
            } else {
                    hpBarImage2.color = hpBarColor;
            }
        }

        switch (currentState){
            case GameState.Waiting:
                HandleWaitingState();
                break;

            case GameState.Countdown:
                HandleCountdownState();
                break;

            case GameState.InGame:
                HandleInGameState();
                break;
                
            case GameState.EndGame:
                HandleEndGameState();
                break;
        }
    }

    void SetState(GameState newState) {
        currentState = newState;
        switch (newState) {
            case GameState.Waiting:
                // 대기 상태 초기화 코드
                Debug.Log("Game is now in Waiting state");
                break;
            case GameState.Countdown:
                // 카운트다운 상태 초기화 코드
                Debug.Log("Game is now in Countdown state");
                break;
            case GameState.InGame:
                // 인게임 상태 초기화 코드
                Debug.Log("Game is now in InGame state");
                break;
            case GameState.EndGame:
                // 종료 상태 초기화 코드
                Debug.Log("Game is now in EndGame state");
                break;
        }
    }

    void ProcessData(string data)
    {
        // 받은 데이터 필터링, 상태 변수 업데이트
        Debug.Log("Received data: " + data);

        if (data.Contains("HeadA"))
        {
            playerA.Hit = true;
        }
        if (data.Contains("HeadB"))
        {
            playerB.Hit = true;
        }
        if (data.Contains("ShootA"))
        {
            playerA.Shoot = true;
        }
        if (data.Contains("ShootB"))
        {
            playerB.Shoot = true;
        }
    }



    void HandleWaitingState() {
        bgmWaitTimer += Time.deltaTime;

        if(bgmWaitTimer > 289.0) bgmWaitPlayFlag = true;
        waitingUi.SetActive(true);
        endUi.SetActive(false);
        countdownTimer.SetActive(false);
        endUi.SetActive(false);
        phase.countdownTimer = 0;


        if ((playerA.Shoot || playerB.Shoot) && phase.countdownTimer < 0.5) { //게임 시작 조건
            playerA.Shoot = false;
            playerB.Shoot = false;
            audioStartupFlag = true;
            bgmWaitStopFlag = true;
            phase.showCountdownTimer = countCheck = 3;
            portC.WriteLine("ledon");
            portA.WriteLine("Countdown");
            portB.WriteLine("Countdown");

            SetState(GameState.Countdown);  // 카운트다운 상태로 전환
            phase.countdownTimer = 5;
            countdownTimer.SetActive(true);
            initPlayers();
        }
    }


    void HandleCountdownState() {
        if(phase.showCountdownTimer != countCheck && phase.showCountdownTimer != 0) {
            audioCountFlag = true; 
            countCheck = phase.showCountdownTimer;
        }
        
        playerA.bullet = 20;
        playerB.bullet = 20;
        playerA.shotCommand = 11;
        playerB.shotCommand = 10;
        audioTransitionFlag = true;
        bgmStatStopFlag = true;
        bgmBattlePlayFlag = true;

        if(phase.showCountdownTimer != countCheck && phase.showCountdownTimer != 0 && phase.showCountdownTimer < 6) {
            audioCountFlag = true; 
            countCheck = phase.showCountdownTimer;
        }
        
        waitingUi.SetActive(false);
        gameCountdownUi.SetActive(true);
        countdownTimer.SetActive(false);

        if (phase.countdownTimer <= 0.1) {
            SetState(GameState.InGame);  // 인게임 상태로 전환
            phase.countdownTimer = 60; //게임 제한시간 (4페이즈 시간)
            portA.WriteLine("InGame");
            portB.WriteLine("InGame");
            playerA.Shoot = false;
            playerB.Shoot = false;
        }
    }


    void HandleInGameState()
    {
        ////////// joyA, B에 시작 신호 주기: 아두이노에선 기본 자세 잡아줘야 함

        gameCountdownUi.SetActive(false);
        inGameUi.SetActive(true);
        countdownTimer.SetActive(true);

        // playerA와 playerB 각각을 처리
        HandlePlayerA();
        HandlePlayerB();

        // HP 체크 및 업데이트, 센서 값 처리 등
        CheckSensorAndUpdateHP();

        if (phase.countdownTimer <= 0.1 || playerA.hp <= 0 || playerB.hp <= 0 || playerA.bullet + playerB.bullet <= 0)
        {
            SetState(GameState.EndGame);  // 종료 상태로 전환
            phase.countdownTimer = 10; // 결과 출력 시간 (5페이즈 시간)
            audioTransitionFlag = true;
            bgmBattleStopFlag = true;
            bgmEndPlayFlag = true;
        }


    }

    void HandlePlayerA()
    {
        // 발사 버튼 처리
        if (playerA.Shoot && playerA.shotDelay < 0.1f && playerA.bullet > 0) // 올바른 발사
        {
            portC.WriteLine("ShootA");
            playerA.shotDelay = 3.0f;
            playerA.bullet--;
            Debug.Log("A 발사");
        }
        else if (playerA.Shoot && playerA.bullet <= 0 && playerA.depletedSoundDelay <= 0) // 총알 없음
        {
            audioDepletedFlag = true;
            playerA.depletedSoundDelay = 1;
            Debug.Log("A 총알 없음1");
        }
        else if (playerA.Shoot && playerA.bullet <= 0 && playerA.depletedSoundDelay > 0) // 총알 없음 + 오디오 출력중
        {
            playerA.depletedSoundDelay = 1;
            Debug.Log("A 총알 없음2");
        }
        playerA.Shoot = false;

        // HP 및 탄약 슬라이더 업데이트
        hpSlider1.GetComponent<Slider>().value = playerA.hp;
        bulletSlider1.GetComponent<Slider>().value = playerA.bullet;
    }

    void HandlePlayerB()
    {
        // 발사 버튼 처리
        if (playerB.Shoot && playerB.shotDelay < 0.1f && playerB.bullet > 0) 
        {
            portC.WriteLine("ShootB");
            playerB.shotDelay = 3.0f;
            playerB.bullet--;
            Debug.Log("B 발사");
        }
        else if (playerB.Shoot && playerB.bullet <= 0 && playerB.depletedSoundDelay <= 0) 
        {
            audioDepletedFlag = true;
            playerB.depletedSoundDelay = 1;
            Debug.Log("B 총알 없음1");
        }
        else if (playerB.Shoot && playerB.bullet <= 0 && playerB.depletedSoundDelay > 0) // 총알 없음 + 오디오 출력중
        {
            playerB.depletedSoundDelay = 1;
            Debug.Log("B 총알 없음2");
        }
        playerB.Shoot = false;

        // HP 및 탄약 슬라이더 업데이트
        hpSlider2.GetComponent<Slider>().value = playerB.hp;
        bulletSlider2.GetComponent<Slider>().value = playerB.bullet;
    }


    void HandleEndGameState() {
        inGameUi.SetActive(false);
        endUi.SetActive(true);
        countdownTimer.SetActive(false);

        playerA.Shoot = false;
        playerB.Shoot = false;

        if((int)playerA.hp == (int)playerB.hp) {
            winImage.SetActive(false);
            drawImage.SetActive(true);
            playerOneLogo.SetActive(false);
            playerTwoLogo.SetActive(false);
            nameAxia.SetActive(false);
            nameRic.SetActive(false);
        } else {
            winImage.SetActive(true);
            drawImage.SetActive(false);
            playerOneLogo.SetActive(playerA.hp > playerB.hp);
            nameRic.SetActive(playerA.hp > playerB.hp);
            playerTwoLogo.SetActive(playerA.hp < playerB.hp);
            nameAxia.SetActive(playerA.hp < playerB.hp);
            playerA.winner = (playerA.hp > playerB.hp);
            playerB.winner = (playerA.hp < playerB.hp);
        }


        if (phase.countdownTimer <= 0.1) {
            portA.WriteLine("End");
            portB.WriteLine("End");
            portC.WriteLine("ledoff");
            SetState(GameState.Waiting);  // 다시 대기 상태로 전환
            audioTransitionFlag = true;
            bgmEndStopFlag = true;
            bgmWaitPlayFlag = true;
            phase.countdownTimer = 5;
        }
        
        if(playerA.winner) {
            if(A_flag){
                A_flag = false;
                Debug.Log("playerA WIN!!");
                portA.WriteLine("Win");
                portB.WriteLine("Lose");
            }
        }
        else if(playerB.winner){
            if(B_flag){
                B_flag = false;
                Debug.Log("playerB WIN!!");
                portA.WriteLine("Lose");
                portB.WriteLine("Win");
            }
        }
        else{
            if(C_flag){
                C_flag = false;
                Debug.Log("DRAW!!");
                portA.WriteLine("Lose");
                portB.WriteLine("Lose");
            }
        }
    }


    void CheckSensorAndUpdateHP(){

        if (playerA.Hit || playerB.Hit){  // 센서가 감지된 경우
            if(playerA.Hit && playerA.damagedTimer < 0.1f) 
            {   
                playerA.Hit = false;
                portC.WriteLine("HeadA");
                playerA.hp -= GENERAL_DAMAGE;
                playerA.damagedTimer = 0.75f;  // 일정 시간 동안 다시 맞지 않도록 설정
                audioHitFlag = true;  // 맞았을 때 소리 재생 플래그
                playerA.hitEffectTimer = 15f;  // 시각적 효과 타이머
            }   
            else if(playerB.Hit && playerB.damagedTimer < 0.1f) {
                playerB.Hit = false;
                portC.WriteLine("HeadB");
                playerB.hp -= GENERAL_DAMAGE;
                playerB.damagedTimer = 0.75f;
                audioHitFlag = true;
                playerB.hitEffectTimer = 15f;
            }
        }
    }

    private void initPlayers(){
        playerA = new PlayerStatus();
        playerB = new PlayerStatus();
        playerA.bullet = playerB.bullet = 1;
        playerA.hp = playerB.hp = MAX_HP;
        playerA.winner = false;
        playerB.winner = false;
        A_flag = true;
        B_flag = true;
        C_flag = true;
    }

    void OnDestroy(){
        if (serialThread != null && serialThread.IsAlive)
        {
            isRunning = false;
            serialThread.Join();  // 스레드 종료 대기
        }
        portA.WriteLine("PortOut");
        portB.WriteLine("PortOut");
        portC.WriteLine("PortOut");

        if (portA != null && portA.IsOpen) portA.Close();
        if (portB != null && portB.IsOpen) portB.Close();
        if (portC != null && portC.IsOpen) portC.Close();
    }
}