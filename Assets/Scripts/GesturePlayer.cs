using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Procedural gesture system for CC3 rigs.
public class GesturePlayer : MonoBehaviour
{
    public enum GestureType
    {
        Nod, ShakeHead, Shrug, Wave,
        PointLeft, PointRight,
        Bow, ThumbsUp
    }

    // Left arm raise — X negated relative to right (mirrored bones)
    static readonly Vector3 RClavicleRaise = new(0,  20, 0);
    static readonly Vector3 RUpperarmRaise = new(70,  0, 0);

    [SerializeField] Vector3 pointLClavicle = new(0, -20, 0);
    [SerializeField] Vector3 pointLUpperarm = new(-70, 0, 0);
    [SerializeField] Vector3 pointRClavicle = new(0,  20, 0);
    [SerializeField] Vector3 pointRUpperarm = new( 70, 0, 0);

    [SerializeField] Vector3 waveForearmBase = new(90, 0, 90);
    [SerializeField] Vector3 waveHandBase    = new(0, 0, 0);
    [SerializeField] int   waveAxis = 0;
    [SerializeField] float waveMin  = -30f;
    [SerializeField] float waveMax  =  30f;

    [SerializeField] Vector3 shrugLClavicle = new(0, 0,  20);
    [SerializeField] Vector3 shrugRClavicle = new(0, 0, -20);

    static readonly Vector3 BowSpine1 = new(25, 0, 0);
    static readonly Vector3 BowSpine2 = new(20, 0, 0);
    static readonly Vector3 BowHead   = new(-20, 0, 0);

    // ── Bone names ────────────────────────────────────────────────────────────
    const string Head      = "CC_Base_Head";
    const string Neck      = "CC_Base_NeckTwist01";
    const string Spine1    = "CC_Base_Spine01";
    const string Spine2    = "CC_Base_Spine02";
    const string LClavicle = "CC_Base_L_Clavicle";
    const string RClavicle = "CC_Base_R_Clavicle";
    const string LUpperarm = "CC_Base_L_Upperarm";
    const string RUpperarm = "CC_Base_R_Upperarm";
    const string LForearm  = "CC_Base_L_Forearm";
    const string RForearm  = "CC_Base_R_Forearm";
    const string RHand     = "CC_Base_R_Hand";

    static readonly string[] TrackedBones =
    {
        Head, Neck, Spine1, Spine2,
        LClavicle, RClavicle,
        LUpperarm, RUpperarm,
        LForearm, RForearm,
        RHand
    };

    readonly Dictionary<string, Vector3>   _offsets = new();
    readonly Dictionary<string, Transform> _bones   = new();
    Coroutine _activeGesture;

    void Start()
    {
        foreach (var name in TrackedBones)
        {
            _bones[name]   = FindBoneRecursive(transform, name);
            _offsets[name] = Vector3.zero;
            if (_bones[name] == null)
                Debug.LogWarning($"[GesturePlayer] Bone not found: {name}");
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Play(GestureType gesture)
    {
        if (_activeGesture != null) StopCoroutine(_activeGesture);
        ResetOffsets();
        _activeGesture = gesture switch
        {
            GestureType.Nod        => StartCoroutine(NodRoutine()),
            GestureType.ShakeHead  => StartCoroutine(ShakeHeadRoutine()),
            GestureType.Shrug      => StartCoroutine(ShrugRoutine()),
            GestureType.Wave       => StartCoroutine(WaveRoutine()),
            GestureType.PointLeft  => StartCoroutine(PointRoutine(left: true)),
            GestureType.PointRight => StartCoroutine(PointRoutine(left: false)),
            GestureType.Bow        => StartCoroutine(BowRoutine()),
            GestureType.ThumbsUp   => StartCoroutine(ThumbsUpRoutine()),
            _                      => null
        };
    }

    public bool TryPlay(string command)
    {
        GestureType? g = command switch
        {
            "nod"         => GestureType.Nod,
            "shake_head"  => GestureType.ShakeHead,
            "shrug"       => GestureType.Shrug,
            "wave"        => GestureType.Wave,
            "point_left"  => GestureType.PointLeft,
            "point_right" => GestureType.PointRight,
            "bow"         => GestureType.Bow,
            "thumbs_up"   => GestureType.ThumbsUp,
            _             => null
        };
        if (g == null) return false;
        Play(g.Value);
        return true;
    }

    public void StopGesture()
    {
        if (_activeGesture != null) { StopCoroutine(_activeGesture); _activeGesture = null; }
        ResetOffsets();
    }

    // ── LateUpdate: additive on top of Animator ───────────────────────────────

    void LateUpdate()
    {
        foreach (var (name, offset) in _offsets)
        {
            if (offset == Vector3.zero) continue;
            if (_bones.TryGetValue(name, out var bone) && bone != null)
                bone.localRotation *= Quaternion.Euler(offset);
        }
    }

    // ── Gestures ──────────────────────────────────────────────────────────────

    IEnumerator NodRoutine()
    {
        for (int i = 0; i < 2; i++)
        {
            yield return Tween(0.15f, (Head, new Vector3(22, 0, 0)), (Neck, new Vector3(8, 0, 0)));
            yield return new WaitForSeconds(0.05f);
            yield return Tween(0.15f, (Head, Vector3.zero), (Neck, Vector3.zero));
            yield return new WaitForSeconds(0.08f);
        }
        _activeGesture = null;
    }

    IEnumerator ShakeHeadRoutine()
    {
        foreach (float y in new[] { -18f, 18f, -18f, 18f, 0f })
            yield return Tween(0.13f,
                (Head, new Vector3(0, y, 0)),
                (Neck, new Vector3(0, y * 0.4f, 0)));
        _activeGesture = null;
    }

    IEnumerator ShrugRoutine()
    {
        yield return Tween(0.2f, (LClavicle, shrugLClavicle), (RClavicle, shrugRClavicle));
        yield return new WaitForSeconds(0.5f);
        yield return Tween(0.25f, (LClavicle, Vector3.zero), (RClavicle, Vector3.zero));
        _activeGesture = null;
    }

    IEnumerator WaveRoutine()
    {
        yield return Tween(0.3f,
            (RClavicle, RClavicleRaise),
            (RUpperarm, RUpperarmRaise),
            (RForearm,  waveForearmBase),
            (RHand,     waveHandBase));

        float[] waveSeq = { waveMax, waveMin, waveMax, waveMin, waveMax, 0f };
        foreach (float v in waveSeq)
        {
            Vector3 target = waveHandBase;
            target[waveAxis] += v;
            yield return TweenOne(RHand, target, 0.13f);
        }

        yield return Tween(0.35f,
            (RClavicle, Vector3.zero), (RUpperarm, Vector3.zero),
            (RForearm,  Vector3.zero), (RHand,     Vector3.zero));
        _activeGesture = null;
    }

    IEnumerator PointRoutine(bool left)
    {
        string clav   = left ? LClavicle : RClavicle;
        string ua     = left ? LUpperarm : RUpperarm;
        Vector3 clavT = left ? pointLClavicle : pointRClavicle;
        Vector3 uaT   = left ? pointLUpperarm : pointRUpperarm;

        yield return Tween(0.3f, (clav, clavT), (ua, uaT));
        yield return new WaitForSeconds(1.0f);
        yield return Tween(0.35f, (clav, Vector3.zero), (ua, Vector3.zero));
        _activeGesture = null;
    }

    IEnumerator BowRoutine()
    {
        yield return Tween(0.4f, (Spine1, BowSpine1), (Spine2, BowSpine2), (Head, BowHead));
        yield return new WaitForSeconds(0.6f);
        yield return Tween(0.4f, (Spine1, Vector3.zero), (Spine2, Vector3.zero), (Head, Vector3.zero));
        _activeGesture = null;
    }

    IEnumerator ThumbsUpRoutine()
    {
        yield return Tween(0.3f,
            (RClavicle, RClavicleRaise),
            (RUpperarm, RUpperarmRaise),
            (RForearm,  new Vector3(0, -60, 0)));
        yield return new WaitForSeconds(1.0f);
        yield return Tween(0.35f,
            (RClavicle, Vector3.zero), (RUpperarm, Vector3.zero), (RForearm, Vector3.zero));
        _activeGesture = null;
    }

    // ── Tween helpers ─────────────────────────────────────────────────────────

    IEnumerator TweenOne(string bone, Vector3 target, float duration)
    {
        var start = _offsets[bone];
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            _offsets[bone] = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }
        _offsets[bone] = target;
    }

    IEnumerator Tween(float duration, params (string bone, Vector3 target)[] targets)
    {
        var starts = new Vector3[targets.Length];
        for (int i = 0; i < targets.Length; i++)
            starts[i] = _offsets[targets[i].bone];

        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float a = t / duration;
            for (int i = 0; i < targets.Length; i++)
                _offsets[targets[i].bone] = Vector3.Lerp(starts[i], targets[i].target, a);
            yield return null;
        }
        for (int i = 0; i < targets.Length; i++)
            _offsets[targets[i].bone] = targets[i].target;
    }

    void ResetOffsets()
    {
        foreach (var key in TrackedBones)
            _offsets[key] = Vector3.zero;
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
