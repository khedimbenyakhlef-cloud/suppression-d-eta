const express = require('express');
const path = require('path');
const { createServer } = require('http');

const app = express();
const httpServer = createServer(app);
const PORT = process.env.PORT || 3000;

// Serve static files
app.use(express.static(path.join(__dirname, 'public')));

// Health check for Render
app.get('/health', (req, res) => {
  res.json({ status: 'ok', game: 'Suppression Déta', version: '1.0.0' });
});

// All routes serve the game
app.get('*', (req, res) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

httpServer.listen(PORT, () => {
  console.log(`🎮 Suppression Déta — Serveur lancé sur port ${PORT}`);
  console.log(`🌐 http://localhost:${PORT}`);
});
