const express = require('express');
const bodyParser = require('body-parser');
const cors = require('cors');
const axios = require('axios');
require('dotenv').config();

const app = express();
const PORT = 3000;

app.use(cors());
app.use(bodyParser.json());

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

app.get('/api/health', (req, res) => {
    res.json({ status: 'ok' });
});

app.listen(PORT, () => {
    console.log(`Server is running on http://localhost:${PORT}`);
});