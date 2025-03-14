const express = require('express');
const bodyParser = require('body-parser');
const cors = require('cors');
const axios = require('axios');
const path = require('path');
const fs = require('fs');
const WebSocket = require('ws');
require('dotenv').config();

const app = express();
const PORT = 3000;

const wss = new WebSocket.Server({ noServer: true });
const clients = new Set();

wss.on('connection', (ws) => {
  clients.add(ws);
  
  ws.on('message', (message) => {
    console.log('Received message:', message.toString());
  });
  
  ws.on('close', () => {
    clients.delete(ws);
  });

  ws.send(JSON.stringify({ type: 'initial_state', data: { botState: currentBotState } }));
});

app.use(cors());
app.use(bodyParser.json());
app.use(express.static(path.join(__dirname, 'public')));

let currentBotState = {
    isFollowing: false,
    currentPlayer: null,
    followingPath: false,
    pathPositions: [],
    pathColor: "#FF0000",
    directionColor: "#0000FF",
    lineAlpha: 0.8,
    isRecording: false,
    isReplaying: false,
    fleeEnabled: false,
    isTagging: false,
    logMessages: []
  };

app.post('/api/paths', (req, res) => {
    try {
      const { path } = req.body;
      
      if (!Array.isArray(path)) {
        return res.status(400).json({ error: 'Path must be an array' });
      }

      currentBotState.pathPositions = path;
      broadcastToClients({ type: 'path_update', data: { pathPositions: path } });
      
      res.json({ success: true });
    } catch (error) {
      console.error('Error processing path data:', error);
      res.status(500).json({ error: 'Failed to process path data' });
    }
  });

app.get('/', (req, res) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

app.get('/api/health', (req, res) => {
  res.json({ status: 'ok' });
});

app.get('/api/botstate', (req, res) => {
  res.json(currentBotState);
});

app.post('/api/botstate', (req, res) => {
  const newState = req.body;
  currentBotState = { ...currentBotState, ...newState };

  broadcastToClients({ type: 'state_update', data: currentBotState });
  
  res.json({ success: true, currentState: currentBotState });
});

app.post('/api/text-to-speech', async (req, res) => {
  try {
    const { text, voice = 'en-US', language = 'en-US' } = req.body;
    
    if (!text) {
      return res.status(400).json({ error: 'Text is required' });
    }

    const response = await axios.get('https://api.voicerss.org/', {
      params: {
        key: process.env.VOICERSS_API_KEY,
        src: text,
        hl: language,
        v: voice,
        r: '0',
        c: 'mp3',
        f: '44khz_16bit_stereo'
      },
      responseType: 'arraybuffer'
    });
    
    const audioData = Buffer.from(response.data).toString('base64');
    res.json({ audioData });
    
  } catch (error) {
    console.error('Error in text-to-speech conversion:', error.message);
    if (error.response) {
      console.error('Response status:', error.response.status);
      console.error('Response data:', error.response.data);
    }
    
    res.status(500).json({ 
      error: 'Failed to convert text to speech',
      details: error.message,
      suggestion: 'You may need to register for a VoiceRSS API key at https://www.voicerss.org/'
    });
  }
});

app.get('/api/paths', (req, res) => {
  res.json({ paths: currentBotState.pathPositions });
});

app.post('/api/paths', (req, res) => {
  const { path } = req.body;
  currentBotState.pathPositions = path;
  broadcastToClients({ type: 'path_update', data: { pathPositions: path } });
  res.json({ success: true });
});

app.delete('/api/paths', (req, res) => {
  currentBotState.pathPositions = [];
  broadcastToClients({ type: 'path_update', data: { pathPositions: [] } });
  res.json({ success: true });
});

app.post('/api/follow', (req, res) => {
  const { playerId, playerName } = req.body;
  currentBotState.isFollowing = true;
  currentBotState.currentPlayer = { id: playerId, name: playerName };
  broadcastToClients({ type: 'follow_update', data: { isFollowing: true, currentPlayer: currentBotState.currentPlayer } });
  res.json({ success: true });
});

app.post('/api/stopFollow', (req, res) => {
  currentBotState.isFollowing = false;
  broadcastToClients({ type: 'follow_update', data: { isFollowing: false } });
  res.json({ success: true });
});

app.post('/api/record/start', (req, res) => {
  currentBotState.isRecording = true;
  broadcastToClients({ type: 'recording_update', data: { isRecording: true } });
  res.json({ success: true });
});

app.post('/api/record/stop', (req, res) => {
  currentBotState.isRecording = false;
  broadcastToClients({ type: 'recording_update', data: { isRecording: false } });
  res.json({ success: true });
});

app.post('/api/replay/start', (req, res) => {
  currentBotState.isReplaying = true;
  broadcastToClients({ type: 'replay_update', data: { isReplaying: true } });
  res.json({ success: true });
});

app.post('/api/replay/stop', (req, res) => {
  currentBotState.isReplaying = false;
  broadcastToClients({ type: 'replay_update', data: { isReplaying: false } });
  res.json({ success: true });
});

app.get('/api/presets', (req, res) => {
  const presetDir = path.join(__dirname, 'presets');

  if (!fs.existsSync(presetDir)) {
    fs.mkdirSync(presetDir, { recursive: true });
  }
  
  fs.readdir(presetDir, (err, files) => {
    if (err) {
      return res.status(500).json({ error: 'Failed to read presets' });
    }
    
    const presetFiles = files.filter(file => file.endsWith('.json'));
    res.json({ presets: presetFiles });
  });
});

app.post('/api/presets', (req, res) => {
  const { name, state } = req.body;
  
  if (!name) {
    return res.status(400).json({ error: 'Preset name is required' });
  }
  
  const presetDir = path.join(__dirname, 'presets');
  if (!fs.existsSync(presetDir)) {
    fs.mkdirSync(presetDir, { recursive: true });
  }
  
  const presetPath = path.join(presetDir, `${name}.json`);
  fs.writeFile(presetPath, JSON.stringify(state || currentBotState, null, 2), (err) => {
    if (err) {
      return res.status(500).json({ error: 'Failed to save preset' });
    }
    
    res.json({ success: true, name });
  });
});

app.get('/api/presets/:name', (req, res) => {
  const { name } = req.params;
  const presetPath = path.join(__dirname, 'presets', `${name}.json`);
  
  fs.readFile(presetPath, 'utf8', (err, data) => {
    if (err) {
      return res.status(404).json({ error: 'Preset not found' });
    }
    
    try {
      const preset = JSON.parse(data);
      res.json(preset);
    } catch (error) {
      res.status(500).json({ error: 'Invalid preset data' });
    }
  });
});

app.post('/api/logs', (req, res) => {
  const { message } = req.body;
  const timestamp = new Date().toISOString();
  
  currentBotState.logMessages.push({ timestamp, message });
  console.log(`[${timestamp}] ${message}`);
  if (currentBotState.logMessages.length > 100) {
    currentBotState.logMessages.shift();
  }
  
  broadcastToClients({ type: 'log_update', data: { logs: currentBotState.logMessages } });
  res.json({ success: true });
});

app.post('/api/save-path', (req, res) => {
    const { name } = req.body;
    follower.SavePreset(name);
    res.json({ success: true, message: 'Path saved successfully' });
});

app.get('/api/load-path/:name', (req, res) => {
    const { name } = req.params;
    follower.LoadPreset(name);
    res.json({ success: true, message: 'Path loaded successfully' });
});

function broadcastToClients(data) {
  const message = JSON.stringify(data);
  clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) {
      client.send(message);
    }
  });
}

const server = app.listen(PORT, () => {
  console.log(`Server is running on http://localhost:${PORT}`);
});

server.on('upgrade', (request, socket, head) => {
  wss.handleUpgrade(request, socket, head, (ws) => {
    wss.emit('connection', ws, request);
  });
});