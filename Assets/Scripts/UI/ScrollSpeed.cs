using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
public class ScrollSpeed : MonoBehaviour
{
    private UIDocument rootUI;
    private ScrollView targetScrollView;
    public float ScrollFactor = 250;
    void Start()
    {
        rootUI = GetComponent<UIDocument>();
        targetScrollView = rootUI.rootVisualElement.Q<ScrollView>("Chat");
        targetScrollView.RegisterCallback<WheelEvent>((evt) =>
        {
            targetScrollView.scrollOffset = new Vector2(0, targetScrollView.scrollOffset.y + ScrollFactor * evt.delta.y);
            evt.StopPropagation();
        });
    }
}
