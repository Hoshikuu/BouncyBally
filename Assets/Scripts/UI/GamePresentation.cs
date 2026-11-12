using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Controla la interfaz existente en escena. Nunca crea objetos ni componentes.</summary>
[DefaultExecutionOrder(200)]
public class GamePresentation : MonoBehaviour
{
    public static GamePresentation Instance { get; private set; }
    [SerializeField] private GameManager game;
    [SerializeField] private BallSpawner spawner;
    [SerializeField] private SkillTreeController tree;
    [SerializeField] private PinWorkshopUI workshop;
    [SerializeField] private ProgressionUI progression;
    [SerializeField] private BatchConfigurationUI batchConfiguration;
    [SerializeField] private GameObject menu,settings;
    [SerializeField] private RectTransform batchContent;
    [SerializeField] private BallBatchCard[] cards;
    [SerializeField] private Button pinButton,skillsButton,settingsButton,playButton,menuSettingsButton;
    [SerializeField] private Button goalsButton,landingsButton,motionButton,backSettingsButton,mainMenuButton;
    [SerializeField] private TMP_Text gameTitle,motionLabel,playLabel;
    private bool initialized,played,settingsFromMenu,returnToWorkshop;
    private Action progressChanged;
    private Action<double> batchCompleted;
    private readonly List<KeyValuePair<Button,UnityAction>> listeners=new List<KeyValuePair<Button,UnityAction>>();
    public bool WorkshopOpen=>workshop!=null&&workshop.IsOpen;
    private void Awake()=>Instance=this;
    private bool HasReferences()=>game!=null&&spawner!=null&&tree!=null&&workshop!=null&&progression!=null&&batchConfiguration!=null&&
        menu!=null&&settings!=null&&batchContent!=null&&cards!=null&&
        gameTitle!=null&&motionLabel!=null&&playLabel!=null&&
        pinButton!=null&&skillsButton!=null&&settingsButton!=null&&playButton!=null&&menuSettingsButton!=null&&
        goalsButton!=null&&landingsButton!=null&&motionButton!=null&&backSettingsButton!=null&&
        mainMenuButton!=null;
    private void Start()
    {
        if(!HasReferences()||game.Settings==null)
        {Debug.LogError("GamePresentation has missing scene references.",this);enabled=false;return;}
        if(!tree.Initialize()||!workshop.Initialize()||!progression.Initialize()||!batchConfiguration.Initialize())
        {Debug.LogError("Scene UI is incomplete. Run Bouncy Bally > Run checks.",this);enabled=false;return;}
        foreach(BallBatchCard card in cards)
            if(card==null||!card.Initialize()){Debug.LogError("Missing batch scene view.",this);enabled=false;return;}
        Bind(skillsButton,OpenSkills);Bind(pinButton,OpenWorkshop);Bind(settingsButton,()=>OpenSettings(false));
        Bind(playButton,Play);Bind(menuSettingsButton,()=>OpenSettings(true));
        Bind(goalsButton,()=>progression.OpenGoals());Bind(landingsButton,()=>progression.OpenStats());
        Bind(motionButton,ToggleMotion);Bind(backSettingsButton,CloseSettings);Bind(mainMenuButton,ShowMainMenu);
        progressChanged=Refresh;batchCompleted=BatchCompleted;
        game.ProgressChanged+=progressChanged;spawner.BatchCompleted+=batchCompleted;
        initialized=true;goalsButton.gameObject.SetActive(game.Settings.challenges != null && game.Settings.challenges.Length > 0);gameTitle.text=game.Settings.gameTitle;MotionLabel();Refresh();ShowMainMenu();
    }
    private void Bind(Button button,UnityAction action)
    {button.onClick.AddListener(action);listeners.Add(new KeyValuePair<Button,UnityAction>(button,action));}
    private void OpenSkills(){if(!menu.activeSelf&&!settings.activeSelf&&!progression.IsOpen)CloseWindows();tree.Open();}
    private void OpenWorkshop(){CloseWindows();workshop.Open();}
    private void Refresh()
    {
        if(!initialized)return;
        pinButton.gameObject.SetActive(game.WorkshopUnlocked);
        int index=0;
        foreach(BallBatchCard card in cards)
        {
            bool unlocked=spawner.IsBatchUnlocked(card.BatchIndex);card.SetVisible(unlocked);if(!unlocked)continue;
            card.Rect.sizeDelta=new Vector2(card.Rect.sizeDelta.x,game.Settings.batchCardHeight);
            card.Rect.anchoredPosition=new Vector2(0,-index++*(game.Settings.batchCardHeight+16));
        }
        batchContent.sizeDelta=new Vector2(0,index*(game.Settings.batchCardHeight+16));
    }
    public bool SelectPin(Pin pin)=>initialized&&workshop.Select(pin);
    private void ToggleMotion(){GaltonEffects.ReducedMotion=!GaltonEffects.ReducedMotion;MotionLabel();}
    private void MotionLabel()=>motionLabel.text="MOTION "+(GaltonEffects.ReducedMotion?"LOW":"HIGH");
    public void RequestLaunch(int batchIndex)
    {
        if (initialized) spawner.TryLaunch(batchIndex);
    }
    public void OpenBatchConfiguration(GameObject root)
    {
        if (!initialized || menu.activeSelf || settings.activeSelf) return;
        CloseWindows(); root.SetActive(true); root.transform.SetAsLastSibling();
    }
    public void OpenGacha(){if(!initialized||!game.WorkshopUnlocked)return;returnToWorkshop=true;progression.OpenGacha();}
    public void ShowDataWindow(GameObject root)
    {
        if(!initialized||root==null)return;
        bool fromWorkshop=returnToWorkshop;CloseWindows();returnToWorkshop=fromWorkshop;
        settings.SetActive(false);root.SetActive(true);root.transform.SetAsLastSibling();Time.timeScale=0;
    }
    public void CloseDataWindow()
    {
        if(!initialized)return;
        progression.CloseAll();
        if(returnToWorkshop){returnToWorkshop=false;Time.timeScale=1;workshop.Open();}
        else OpenSettings(settingsFromMenu);
    }
    public void OpenSettings(bool fromMenu)
    {
        if(!initialized)return;
        returnToWorkshop=false;settingsFromMenu=fromMenu;CloseWindows();settings.SetActive(true);settings.transform.SetAsLastSibling();Time.timeScale=0;
    }
    private void CloseSettings(){settings.SetActive(false);if(!settingsFromMenu)Time.timeScale=1;}
    private void CloseWindows(){batchConfiguration.Close();tree.Close();workshop.Close();progression.CloseAll();}
    public void ShowMainMenu()
    {
        if(!initialized)return;
        CloseWindows();settings.SetActive(false);menu.SetActive(true);menu.transform.SetAsLastSibling();playLabel.text=played?"RESUME":"PLAY";Time.timeScale=0;
    }
    private void Play(){played=true;menu.SetActive(false);settings.SetActive(false);Time.timeScale=1;}
    private void BatchCompleted(double value)
    {if(value>0&&game.Board!=null)game.Board.GetComponent<GaltonEffects>()?.PlaySlot(game.Board.GetSpawnPosition(),GaltonTheme.Accent,value);}
    private void Update()
    {
        if(!initialized)return;
        bool typing = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            (EventSystem.current.currentSelectedGameObject.GetComponentInParent<TMP_InputField>() != null || EventSystem.current.currentSelectedGameObject.GetComponentInParent<InputField>() != null);
        bool rightClick = false;
#if ENABLE_INPUT_SYSTEM
        rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        rightClick = Input.GetMouseButtonDown(1);
#endif
        if (rightClick && !typing) { if (tree.IsOpen) tree.Close(); else OpenSkills(); }
        bool escape=false;
#if ENABLE_INPUT_SYSTEM
        if (!typing && Time.timeScale > 0 && Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) RequestLaunch(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) RequestLaunch(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) RequestLaunch(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) RequestLaunch(3);
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (!typing && Time.timeScale > 0)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) RequestLaunch(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) RequestLaunch(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) RequestLaunch(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) RequestLaunch(3);
        }
#endif
#if ENABLE_INPUT_SYSTEM
        escape=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        escape=Input.GetKeyDown(KeyCode.Escape);
#endif
        if(!escape)return;
        if(tree.IsOpen)tree.Close();else if(batchConfiguration.IsOpen)batchConfiguration.Close();else if(progression.IsOpen)CloseDataWindow();else if(settings.activeSelf)CloseSettings();else OpenSettings(menu.activeSelf);
    }
    private void OnDestroy()
    {
        // Conserva los delegates creados al inicializar; no enlaza métodos de componentes destruidos.
        if(initialized)
        {
            if(spawner!=null)spawner.BatchCompleted-=batchCompleted;
            if(game!=null)game.ProgressChanged-=progressChanged;
        }
        foreach(var pair in listeners)if(pair.Key!=null)pair.Key.onClick.RemoveListener(pair.Value);
        listeners.Clear();if(Instance==this)Instance=null;Time.timeScale=1;
    }
}
