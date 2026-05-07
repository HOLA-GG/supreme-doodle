from flask import Flask, render_template, request, jsonify, abort
import sqlite3
import json
import base64
from datetime import datetime
from flask_limiter import Limiter
from flask_limiter.util import get_remote_address
from flask_talisman import Talisman
import uuid

app = Flask(__name__)

# --- SEGURIDAD ESTILO CISCO ---
# 1. Limitar el tamaño máximo de los datos recibidos (Evita ataques de memoria)
app.config['MAX_CONTENT_LENGTH'] = 16 * 1024  # Máximo 16KB por petición

# 2. Forzar HTTPS y cabeceras de seguridad (Protección XSS, Clickjacking, CSP)
csp = {
    'default-src': '\'self\'',
    'style-src': [
        '\'self\'',
        '\'unsafe-inline\'',
        'https://fonts.googleapis.com'
    ],
    'font-src': [
        '\'self\'',
        'https://fonts.gstatic.com'
    ],
    'script-src': '\'self\''
}
talisman = Talisman(app, content_security_policy=csp, force_https=False) # False para desarrollo, True en prod

# 3. Rate Limiting (Protección Anti-DDoS / Brute Force)
limiter = Limiter(
    get_remote_address,
    app=app,
    default_limits=["200 per day", "50 per hour"],
    storage_uri="memory://",
)

DB_NAME = "soldiers.db"

def init_db():
    conn = sqlite3.connect(DB_NAME)
    c = conn.cursor()
    c.execute('''CREATE TABLE IF NOT EXISTS soldiers
                 (hw_id TEXT PRIMARY KEY, account_id TEXT, hostname TEXT, 
                  mac_address TEXT, local_ip TEXT, connection_type TEXT, 
                  ssid TEXT, latitude REAL, longitude REAL, 
                  is_alarm INTEGER, alarm_reason TEXT, last_seen TEXT)''')
    
    # Nueva tabla para clientes/empresas
    c.execute('''CREATE TABLE IF NOT EXISTS accounts
                 (account_id TEXT PRIMARY KEY, company_name TEXT, email TEXT, created_at TEXT)''')
                 
    conn.commit()
    conn.close()

init_db()

@app.route('/')
@limiter.limit("20 per minute")
def index():
    # Landing page promocional
    return render_template('landing.html')

@app.route('/login', methods=['GET', 'POST'])
@limiter.limit("10 per minute")
def login():
    if request.method == 'POST':
        account_id = request.form.get('account_id')
        account_id = ''.join(e for e in account_id if e.isalnum() or e in ['_', '-'])
        if account_id:
            # Validar que la cuenta existe
            conn = sqlite3.connect(DB_NAME)
            c = conn.cursor()
            exists = c.execute("SELECT 1 FROM accounts WHERE account_id = ?", (account_id,)).fetchone()
            conn.close()
            
            if exists or account_id == 'DEFAULT_USER':
                from flask import redirect, url_for
                return redirect(url_for('dashboard', account=account_id))
            else:
                return render_template('login.html', error="ID de Organización no encontrado.")
    return render_template('login.html')

@app.route('/register', methods=['GET', 'POST'])
@limiter.limit("5 per minute")
def register():
    if request.method == 'POST':
        company_name = request.form.get('company_name', 'Empresa').strip()[:50]
        email = request.form.get('email', '').strip()[:50]
        
        # Generar un ID único amigable (ej. EMP-4F8A)
        short_uuid = str(uuid.uuid4()).split('-')[0].upper()
        account_id = f"EMP-{short_uuid}"
        
        conn = sqlite3.connect(DB_NAME)
        c = conn.cursor()
        c.execute("INSERT INTO accounts VALUES (?, ?, ?, ?)", 
                 (account_id, company_name, email, datetime.now().strftime("%Y-%m-%d %H:%M:%S")))
        conn.commit()
        conn.close()
        
        return render_template('register_success.html', account_id=account_id, company=company_name)
        
    return render_template('register.html')

@app.route('/dashboard')
@limiter.limit("10 per minute")
def dashboard():
    account = request.args.get('account', 'DEFAULT_USER')
    # Sanitización básica del input
    account = ''.join(e for e in account if e.isalnum() or e in ['_', '-'])
    
    conn = sqlite3.connect(DB_NAME)
    conn.row_factory = sqlite3.Row
    c = conn.cursor()
    
    # Query parametrizado (Protección SQL Injection)
    soldiers = c.execute("SELECT * FROM soldiers WHERE account_id = ? ORDER BY last_seen DESC", (account,)).fetchall()
    conn.close()
    
    formatted_soldiers = [dict(s) for s in soldiers]
    return render_template('dashboard.html', soldiers=formatted_soldiers, now=datetime.now().strftime("%H:%M:%S"), account_name=account)

@app.route('/api/heartbeat', methods=['POST'])
@limiter.limit("5 per minute") # Un soldado no debería reportar más de esto
def heartbeat():
    try:
        data = request.get_json()
        if not data:
            abort(400)

        # Validación estricta de campos requeridos
        required = ['HardwareFingerprint', 'Hostname', 'MacAddress']
        if not all(k in data for k in required):
            return jsonify({"status": "invalid_payload"}), 400

        account_id = data.get('AccountId', 'DEFAULT_USER')[:50] # Limitar longitud
        hw_id = data.get('HardwareFingerprint', 'UNKNOWN')[:100]
        hostname = data.get('Hostname', 'Unknown')[:100]
        mac = data.get('MacAddress', 'Unknown')[:30]
        local_ip = data.get('LocalIp', '0.0.0.0')[:20]
        conn_type = data.get('ConnectionType', 'Unknown')[:20]
        ssid = data.get('CurrentSsid', 'Unknown')[:50]
        lat = float(data.get('Latitude', 0))
        lon = float(data.get('Longitude', 0))
        is_alarm = 1 if data.get('IsAlarmActive', False) else 0
        reason = data.get('AlarmReason', '')[:200]
        last_seen = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

        conn = sqlite3.connect(DB_NAME)
        c = conn.cursor()
        c.execute('''INSERT OR REPLACE INTO soldiers 
                     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)''',
                  (hw_id, account_id, hostname, mac, local_ip, conn_type, ssid, lat, lon, is_alarm, reason, last_seen))
        conn.commit()
        conn.close()

        return jsonify({"status": "secure_received", "t": last_seen}), 200
    except Exception:
        # No revelamos detalles del error al atacante
        return jsonify({"status": "internal_security_error"}), 500

if __name__ == '__main__':
    # Usar puerto 443 en prod con SSL
    app.run(debug=False, port=80)
