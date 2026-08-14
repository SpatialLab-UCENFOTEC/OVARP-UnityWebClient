using UnityEngine;

// Rotates a head bone toward a target each frame after animation plays.
// Works on Generic rigs — no humanoid avatar required.
public class HeadLookAt : MonoBehaviour
{
    [SerializeField] string headBoneName = "CC_Base_Head";
    [SerializeField] Transform target;
    [SerializeField, Range(0, 1)] float weight = 0.6f;
    // Euler offset applied after look-at to correct for bone's local forward axis
    [SerializeField] Vector3 rotationOffset = Vector3.zero;

    Transform _headBone;
    bool _enabled = true;

    void Start()
    {
        _headBone = FindBoneRecursive(transform, headBoneName);
        if (_headBone == null)
            Debug.LogWarning($"[HeadLookAt] Bone '{headBoneName}' not found under {name}");

        if (target == null && Camera.main != null)
            target = Camera.main.transform;
    }

    // LateUpdate runs after Animator — ensures we override the animated rotation
    void LateUpdate()
    {
        if (!_enabled || _headBone == null || target == null) return;

        Vector3 dir = (target.position - _headBone.position).normalized;
        Quaternion delta = Quaternion.FromToRotation(_headBone.forward, dir);
        Quaternion correction = Quaternion.Euler(rotationOffset);
        _headBone.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight)
                             * _headBone.rotation
                             * correction;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Handles "looks" commands from the server
    public void SetLookTarget(string lookCommand)
    {
        switch (lookCommand)
        {
            case "user":
                target = Camera.main != null ? Camera.main.transform : null;
                _enabled = true;
                break;

            case "away":
                _enabled = false;
                break;

            case "agent_beta":
                var beta = GameObject.Find("agent_beta") ?? GameObject.Find("Beta");
                if (beta != null)
                {
                    target = beta.transform;
                    _enabled = true;
                }
                else
                {
                    Debug.LogWarning("[HeadLookAt] agent_beta not found in scene.");
                }
                break;

            default:
                Debug.LogWarning($"[HeadLookAt] Unknown look target: {lookCommand}");
                break;
        }
    }

    static Transform FindBoneRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var r = FindBoneRecursive(child, name);
            if (r != null) return r;
        }
        return null;
    }
}
