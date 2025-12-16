require('dotenv').config();
const express = require('express');
const path = require('path');
const httpProxy = require('http-proxy');

const PORT = process.env.PORT || 3000;
const API_TARGET = process.env.API_TARGET;

if (!API_TARGET) {
  console.error('API_TARGET env var is required (e.g. http://localhost:5001)');
  process.exit(1);
}

const app = express();
const proxy = httpProxy.createProxyServer({
  target: API_TARGET,
  changeOrigin: true,
  ws: true,
  xfwd: true
});

proxy.on('error', (err, req, res) => {
  console.error('Proxy error', err.message);
  if (!res.headersSent) {
    res.status(502).send('Proxy error');
  }
});

app.set('view engine', 'ejs');
app.set('views', path.join(__dirname, 'views'));

// Proxy first so we don't consume the body with parsers before piping to backend.
const proxyPaths = ['/api', '/auth', '/codes', '/diag', '/health', '/ws'];
app.use(proxyPaths, (req, res) => proxy.web(req, res));
app.on('upgrade', (req, socket, head) => proxy.ws(req, socket, head));

app.use(express.json({ limit: '1mb' }));
app.use(express.urlencoded({ extended: true }));

app.get('/', (req, res) => {
  res.render('client', { apiBase: '/api' });
});

app.get('/admin', (req, res) => {
  res.render('admin', { apiBase: '/api' });
});

app.use((req, res) => {
  res.status(404).send('Not found');
});

app.use((err, req, res, next) => {
  console.error(err);
  res.status(500).send('Internal server error');
});

app.listen(PORT, () => {
  console.log(`Frontend-EJS listening on port ${PORT}, proxying API to ${API_TARGET}`);
});
