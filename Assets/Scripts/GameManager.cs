using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private string agentName = "Nova";
    private string userName = "User";
    private string defaultAgentName = "Nova";
    private bool darkMode = false;
    
    [SerializeField]
    private Color userBubbleColor = new Vector4(0f,0.5176471f,1f,1f);
    [SerializeField]
    private Color agentBubbleColor = new Vector4(0f, 0.3882353f, 0.7490196f, 1f);
    public static event Action<string> OnAgentNameChange;
    public static event Action<string> OnUserNameChange;
    public static event Action<Color> OnUserColorChange;
    public static event Action<Color> OnAgentColorChange;
    public static event Action<bool> OnDarkMode;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
        //userBubbleColor = new Color(0f,0.5176471f,1f,1f);
        //agentBubbleColor = new Color(0f, 0.3882353f, 0.7490196f, 1f);
    }

#if !UNITY_WEBGL
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Application.Quit();
        }
    }
#endif

    public void SetAgentName(string name)
    {
        this.agentName = name;
        OnAgentNameChange?.Invoke(name);
    }
    

    public void SetUserName(string name)
    {
        this.userName = name;
        OnUserNameChange?.Invoke(name);
    }

    public void ResetAgentName()
    {
        OnAgentNameChange?.Invoke(defaultAgentName);
    }

    public void SetAgentColor(Color color)
    {
        agentBubbleColor = color;
        OnAgentColorChange?.Invoke(color);
    }

    public void SetUserColor(Color color)
    {
        userBubbleColor = color;
        OnUserColorChange?.Invoke(color);
    }

    public void SetDarkMode(bool state)
    {
        darkMode = state;
        OnDarkMode?.Invoke(state);
    }

    public string GetUserName()
    {
        return userName;
    }

    public string GetAgentName()
    {
        return agentName;
    }
    public string GetDefaultName()
    {
        return defaultAgentName;
    }

    public bool GetDarkMode()
    {
        return darkMode;
    }

    public Color getUserBubbleColor()
    {
        return userBubbleColor;
    }

    public Color getAgentBubbleColor()
    {
        return agentBubbleColor;
    }
}
