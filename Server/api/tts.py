from flask import Flask, request, jsonify, send_file
from flask_cors import CORS
import requests
import base64
import os
from flask import session, render_template, redirect, url_for, flash
import hashlib, time, secrets, uuid, hmac
from datetime import datetime, timedelta
import json, socket, logging
from collections import deque
from pymongo import MongoClient
from pymongo.errors import ConnectionFailure, ServerSelectionTimeoutError
from itertools import islice
import io
import mimetypes
import speech_recognition as sr
import tempfile
import wave

from dotenv import load_dotenv
load_dotenv()

app = Flask(__name__)
CORS(app)

VOICE_RSS_API_KEY = os.getenv('VOICERSS_API_KEY')
if not VOICE_RSS_API_KEY:
    raise RuntimeError("Missing VOICERSS_API_KEY in environment variables")

@app.route('/api/text-to-speech', methods=['POST'])
def text_to_speech():
    try:
        data = request.get_json()
        text = data.get('text')
        voice = data.get('voice', 'en-US')
        language = data.get('language', 'en-US')

        if not text:
            return jsonify({'error': 'Text is required'}), 400

        response = requests.get('https://api.voicerss.org/', params={
            'key': VOICE_RSS_API_KEY,
            'src': text,
            'hl': language,
            'v': voice,
            'r': '0',
            'c': 'mp3',
            'f': '44khz_16bit_stereo'
        })

        if response.status_code != 200:
            return jsonify({
                'error': 'VoiceRSS API error',
                'details': response.text
            }), response.status_code

        audio_data = base64.b64encode(response.content).decode('utf-8')
        return jsonify({ 'audioData': audio_data })

    except Exception as e:
        return jsonify({
            'error': 'Failed to convert text to speech',
            'details': str(e),
            'suggestion': 'You may need to register for a VoiceRSS API key at https://www.voicerss.org/'
        }), 500

if __name__ == '__main__':
    app.run(port=3000, debug=True)
