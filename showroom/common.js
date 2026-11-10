/* 展厅公用：载入模型（不使用 fetch，适配匿名仓库网页模式与本地双击打开） */
(function () {
  var T = window.T;

  function loadScript(src) {
    return new Promise(function (res, rej) {
      var s = document.createElement('script');
      s.src = src;
      s.onload = function () { res(); };
      s.onerror = function () { rej(new Error('载入失败：' + src)); };
      document.head.appendChild(s);
    });
  }

  function b64ToBuffer(parts) {
    var total = 0, i, j, bins = [];
    for (i = 0; i < parts.length; i++) { bins.push(atob(parts[i])); total += bins[i].length; }
    var u = new Uint8Array(total), o = 0;
    for (i = 0; i < bins.length; i++) {
      var b = bins[i];
      for (j = 0; j < b.length; j++) u[o + j] = b.charCodeAt(j);
      o += b.length;
    }
    return u.buffer;
  }

  var loader = null;
  function getLoader() {
    if (!loader) {
      loader = new T.GLTFLoader();
      loader.setMeshoptDecoder(T.MeshoptDecoder);
    }
    return loader;
  }

  // onProgress(0..1)
  function loadGLB(key, onProgress) {
    var man = window.__GLB_MANIFEST[key];
    if (!man) return Promise.reject(new Error('没有这个模型：' + key));
    var have = (window.__GLB && window.__GLB[key]) || [];
    var jobs = [], done = 0;
    for (var i = 0; i < man.parts; i++) {
      if (have[i]) { done++; continue; }
      jobs.push(loadScript('models/' + key + '.' + i + '.js').then(function () {
        done++; if (onProgress) onProgress(done / man.parts);
      }));
    }
    if (onProgress) onProgress(done / man.parts);
    return Promise.all(jobs).then(function () {
      var buf = b64ToBuffer(window.__GLB[key]);
      return new Promise(function (res, rej) {
        T.MeshoptDecoder.ready.then(function () {
          getLoader().parse(buf, '', res, rej);
        });
      });
    });
  }

  function safeHash(set) {
    try {
      if (set === undefined) return decodeURIComponent((location.hash || '').slice(1));
      history.replaceState(null, '', '#' + set);
    } catch (e) { return ''; }
  }

  function hasWebGL() {
    try {
      var c = document.createElement('canvas');
      return !!(window.WebGLRenderingContext && (c.getContext('webgl2') || c.getContext('webgl')));
    } catch (e) { return false; }
  }

  window.SR = { loadGLB: loadGLB, safeHash: safeHash, hasWebGL: hasWebGL };
})();
