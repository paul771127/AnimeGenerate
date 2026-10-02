// 雙機連線：PeerJS (WebRTC DataChannel)，用 4 位數房號配對。
const PREFIX = 'spellbattle-demo-v1-';

// 預設使用 PeerJS 公用信令伺服器；可用網址參數改用自架伺服器：?peerhost=example.com&peerport=9000&peerpath=/myapp
function peerOptions() {
  const q = new URLSearchParams(location.search);
  if (!q.get('peerhost')) return {};
  return {
    host: q.get('peerhost'),
    port: Number(q.get('peerport') || 443),
    path: q.get('peerpath') || '/',
    secure: q.get('peersecure') ? q.get('peersecure') === '1' : location.protocol === 'https:',
  };
}

export class Net {
  constructor({ onMessage, onStatus, onOpen, onClose }) {
    this.onMessage = onMessage;
    this.onStatus = onStatus || (() => {});
    this.onOpen = onOpen || (() => {});
    this.onClose = onClose || (() => {});
    this.conn = null;
    this.peer = null;
    this.isHost = false;
  }

  get connected() { return !!(this.conn && this.conn.open); }

  host() {
    return new Promise((resolve, reject) => {
      const code = String(Math.floor(1000 + Math.random() * 9000));
      this.isHost = true;
      this.peer = new window.Peer(PREFIX + code, peerOptions());
      this.peer.on('open', () => { this.onStatus(`房間 ${code} 已建立，等待對手加入…`); resolve(code); });
      this.peer.on('connection', (c) => {
        if (this.conn && this.conn.open) { c.close(); return; }
        this._bind(c);
      });
      this.peer.on('error', (err) => {
        if (err.type === 'unavailable-id') { this.peer.destroy(); this.host().then(resolve, reject); return; }
        this.onStatus(`連線錯誤：${err.type}`);
        reject(err);
      });
    });
  }

  join(code) {
    return new Promise((resolve, reject) => {
      this.isHost = false;
      this.peer = new window.Peer(peerOptions());
      this.peer.on('open', () => {
        this.onStatus(`正在連線房間 ${code}…`);
        const c = this.peer.connect(PREFIX + code, { reliable: true });
        this._bind(c, resolve);
      });
      this.peer.on('error', (err) => {
        this.onStatus(err.type === 'peer-unavailable' ? `找不到房間 ${code}` : `連線錯誤：${err.type}`);
        reject(err);
      });
    });
  }

  _bind(c, resolve) {
    this.conn = c;
    c.on('open', () => { this.onStatus('✅ 已連線'); this.onOpen(); resolve && resolve(); });
    c.on('data', (d) => this.onMessage(d));
    c.on('close', () => { this.onStatus('❌ 對手已斷線'); this.onClose(); });
  }

  send(msg) {
    if (this.connected) this.conn.send(msg);
  }

  destroy() {
    try { this.peer && this.peer.destroy(); } catch (_) {}
  }
}
