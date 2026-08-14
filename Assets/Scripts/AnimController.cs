using UnityEngine;

public class AnimController : MonoBehaviour
{
    Animator _anim;
    GesturePlayer _gestures;

    void Start()
    {
        _anim     = GetComponent<Animator>();
        _gestures = GetComponent<GesturePlayer>();
    }

    public void StartThinking() => _anim.SetTrigger("Think");

    public void StopThinking()
    {
        _anim.SetTrigger("StopThinking");
        // Force-exit any looping thinking state back to Idle
        _anim.Play("Idle", 0);
    }

    // Routes server action commands:
    //   "thinking" → Animator Think trigger
    //   "idle"     → stop thinking + stop gesture
    //   all others → GesturePlayer procedural gestures
    public void PlayAnimation(string command)
    {
        switch (command)
        {
            case "thinking":
                StartThinking();
                break;

            case "idle":
                StopThinking();
                _gestures?.StopGesture();
                break;


            default:
                if (_gestures != null && _gestures.TryPlay(command)) break;
                // Fallback: treat unknown commands as Animator trigger names
                _anim.SetTrigger(command);
                break;
        }
    }
}
