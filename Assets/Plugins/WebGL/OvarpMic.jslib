// Microphone capture for Unity Web. Unity's Microphone class is absent from the
// WebGL player (UnityEngine.AudioModule.dll ships without it), so capture goes
// through getUserMedia + an AudioWorklet and the PCM is pulled into managed
// memory by WebMicrophoneCapture.cs.
//
// Permission states: 0 = idle, 1 = pending, 2 = granted, 3 = denied.

var OvarpMicLib = {
  $OvarpMic: {
    permission: 0,
    stream: null,
    context: null,
    source: null,
    worklet: null,
    chunks: [],
    total: 0,
    recording: false,

    // The worklet module has to be fetched from a URL; a Blob keeps it inside
    // this single plugin file instead of shipping a separate asset.
    workletSource: [
      'class OvarpMicProcessor extends AudioWorkletProcessor {',
      '  process(inputs) {',
      '    const input = inputs[0];',
      '    if (input && input.length > 0 && input[0] && input[0].length > 0) {',
      '      this.port.postMessage(new Float32Array(input[0]));',
      '    }',
      '    return true;',
      '  }',
      '}',
      'registerProcessor("ovarp-mic-processor", OvarpMicProcessor);'
    ].join('\n'),

    reset: function () {
      OvarpMic.chunks = [];
      OvarpMic.total = 0;
    },

    teardown: function () {
      if (OvarpMic.worklet) {
        OvarpMic.worklet.port.onmessage = null;
        OvarpMic.worklet.disconnect();
        OvarpMic.worklet = null;
      }
      if (OvarpMic.source) {
        OvarpMic.source.disconnect();
        OvarpMic.source = null;
      }
    }
  },

  OvarpMic_RequestPermission: function () {
    if (OvarpMic.permission === 1 || OvarpMic.permission === 2) return;

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      console.error('[OvarpMic] getUserMedia unavailable. A secure context (HTTPS or localhost) is required.');
      OvarpMic.permission = 3;
      return;
    }

    OvarpMic.permission = 1;
    navigator.mediaDevices.getUserMedia({ audio: true }).then(function (stream) {
      OvarpMic.stream = stream;
      OvarpMic.permission = 2;
    }).catch(function (err) {
      console.error('[OvarpMic] Microphone permission denied: ' + err);
      OvarpMic.permission = 3;
    });
  },

  OvarpMic_GetPermissionState: function () {
    return OvarpMic.permission;
  },

  OvarpMic_Start: function () {
    if (OvarpMic.permission !== 2 || OvarpMic.recording) return;

    OvarpMic.reset();

    if (!OvarpMic.context) {
      var AudioCtx = window.AudioContext || window.webkitAudioContext;
      OvarpMic.context = new AudioCtx();
    }
    if (OvarpMic.context.state === 'suspended') {
      OvarpMic.context.resume();
    }

    var blob = new Blob([OvarpMic.workletSource], { type: 'application/javascript' });
    var url = URL.createObjectURL(blob);

    OvarpMic.recording = true;
    OvarpMic.context.audioWorklet.addModule(url).then(function () {
      URL.revokeObjectURL(url);
      if (!OvarpMic.recording) return;

      OvarpMic.source = OvarpMic.context.createMediaStreamSource(OvarpMic.stream);
      OvarpMic.worklet = new AudioWorkletNode(OvarpMic.context, 'ovarp-mic-processor');
      OvarpMic.worklet.port.onmessage = function (event) {
        if (!OvarpMic.recording) return;
        OvarpMic.chunks.push(event.data);
        OvarpMic.total += event.data.length;
      };
      OvarpMic.source.connect(OvarpMic.worklet);
    }).catch(function (err) {
      URL.revokeObjectURL(url);
      OvarpMic.recording = false;
      console.error('[OvarpMic] AudioWorklet failed to start: ' + err);
    });
  },

  OvarpMic_Stop: function () {
    OvarpMic.recording = false;
    OvarpMic.teardown();
  },

  OvarpMic_GetSampleCount: function () {
    return OvarpMic.total;
  },

  OvarpMic_GetSampleRate: function () {
    return OvarpMic.context ? OvarpMic.context.sampleRate : 0;
  },

  // Copies the captured samples into the managed float[] pinned at buffer and
  // clears the JS-side accumulation. Returns how many samples were written.
  OvarpMic_ReadSamples: function (buffer, maxSamples) {
    var written = 0;
    for (var i = 0; i < OvarpMic.chunks.length && written < maxSamples; i++) {
      var chunk = OvarpMic.chunks[i];
      var count = Math.min(chunk.length, maxSamples - written);
      HEAPF32.set(chunk.subarray(0, count), (buffer >> 2) + written);
      written += count;
    }
    OvarpMic.reset();
    return written;
  }
};

autoAddDeps(OvarpMicLib, '$OvarpMic');
mergeInto(LibraryManager.library, OvarpMicLib);
