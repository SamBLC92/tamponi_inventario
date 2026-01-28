import os
import sqlite3
from datetime import datetime, timedelta
from typing import Optional, Dict, Any, List, Callable, Tuple
from functools import wraps
import secrets
import hmac
import hashlib
import json

from flask import (
    Flask, render_template, request, redirect, url_for,
    send_file, jsonify, flash, session
)
from barcode import Code128
from barcode.writer import ImageWriter

APP_DIR = os.path.dirname(os.path.abspath(__file__))
DB_PATH = os.path.join(APP_DIR, "inventory.db")
LABELS_DIR = os.path.join(APP_DIR, "labels")

ssl_file = "//HOMEASSISTANT/ssl/privkey.pem"
ssl_cert = "//HOMEASSISTANT/ssl/fullchain.pem"

# ✅ Soglie GLOBALI (uguali per tutti)
DEFAULT_GLOBAL_WARN_DAYS = 180
DEFAULT_GLOBAL_ALARM_DAYS = 200
SETTINGS_KEY_WARN_DAYS = "global_warn_days"
SETTINGS_KEY_ALARM_DAYS = "global_alarm_days"

# ✅ Barcode: più allungato e meno alto (etichette più basse)
DEFAULT_BARCODE_MODULE_WIDTH = 0.30
DEFAULT_BARCODE_MODULE_HEIGHT = 9.0
DEFAULT_BARCODE_QUIET_ZONE = 6.0
DEFAULT_BARCODE_FONT_SIZE = 9
DEFAULT_BARCODE_TEXT_DISTANCE = 1.5
DEFAULT_BARCODE_WRITE_TEXT = False

SETTINGS_KEY_BARCODE_MODULE_WIDTH = "barcode_module_width"
SETTINGS_KEY_BARCODE_MODULE_HEIGHT = "barcode_module_height"
SETTINGS_KEY_BARCODE_QUIET_ZONE = "barcode_quiet_zone"
SETTINGS_KEY_BARCODE_FONT_SIZE = "barcode_font_size"
SETTINGS_KEY_BARCODE_TEXT_DISTANCE = "barcode_text_distance"
SETTINGS_KEY_BARCODE_WRITE_TEXT = "barcode_write_text"
SETTINGS_KEY_BARCODE_SETTINGS_HASH = "barcode_settings_hash"

# ✅ Password admin (imposta variabile ambiente ADMIN_PASSWORD)
ADMIN_PASSWORD = os.environ.get("ADMIN_PASSWORD", "admin")
START_URL = os.environ.get("START_URL", "http://localhost:8086")
#START_URL = os.environ.get("START_URL", "https://localhost:8086")

app = Flask(__name__)
app.secret_key = os.environ.get("FLASK_SECRET_KEY", secrets.token_hex(32))
app.config["SEND_FILE_MAX_AGE_DEFAULT"] = 60 * 60 * 24


# ---------------------------
# Auth helpers
# ---------------------------
def is_logged_in() -> bool:
    return bool(session.get("admin_logged", False))


def require_admin(view_func: Callable):
    @wraps(view_func)
    def wrapper(*args, **kwargs):
        if not is_logged_in():
            return redirect(url_for("login", next=request.path))
        return view_func(*args, **kwargs)
    return wrapper


@app.context_processor
def inject_auth():
    return {"is_admin": is_logged_in()}


@app.route("/login", methods=["GET", "POST"])
def login():
    if request.method == "POST":
        pw = request.form.get("password", "")
        ok = hmac.compare_digest(pw, ADMIN_PASSWORD)
        if ok:
            session["admin_logged"] = True
            flash("Accesso effettuato.", "ok")
            return redirect(url_for("admin_dashboard"))
        flash("Password errata.", "error")
    return render_template("login.html")


@app.route("/logout")
def logout():
    session.clear()
    flash("Logout effettuato.", "ok")
    return redirect(url_for("swabs"))


# ---------------------------
# Helpers
# ---------------------------
def now_iso() -> str:
    return datetime.now().isoformat(timespec="seconds")


def ensure_dir(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def connect() -> sqlite3.Connection:
    con = sqlite3.connect(DB_PATH)
    con.row_factory = sqlite3.Row
    con.execute("PRAGMA foreign_keys = ON;")
    return con


def parse_iso(ts: str) -> datetime:
    return datetime.fromisoformat(ts)


def date_to_key(d) -> str:
    return d.strftime("%Y-%m-%d")


def iter_dates_inclusive(start_date, end_date):
    cur = start_date
    while cur <= end_date:
        yield cur
        cur += timedelta(days=1)


def calendar_days_between(start_iso: str, end_iso: str) -> int:
    # inclusivo: stesso giorno => 1
    a = parse_iso(start_iso).date()
    b = parse_iso(end_iso).date()
    return (b - a).days + 1


def current_calendar_days(start_iso: str) -> int:
    return calendar_days_between(start_iso, now_iso())


@app.template_filter("it_datetime")
def it_datetime(value: str) -> str:
    try:
        dt = datetime.fromisoformat(value)
        return dt.strftime("%H:%M:%S %d/%m/%Y")
    except Exception:
        return value


def compute_barcode_settings_hash(settings: Dict[str, Any]) -> str:
    payload = json.dumps(settings, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def init_db() -> None:
    ensure_dir(LABELS_DIR)
    with connect() as con:
        con.executescript(
            """
            CREATE TABLE IF NOT EXISTS swabs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                sku TEXT UNIQUE NOT NULL,
                name TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS machines (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT UNIQUE NOT NULL
            );

            -- Stato tampone: in_stock 1=RESO (magazzino), 0=PRESO (in uso)
            -- machine_id valorizzato solo quando PRESO
            CREATE TABLE IF NOT EXISTS swab_state (
                swab_id INTEGER PRIMARY KEY,
                in_stock INTEGER NOT NULL DEFAULT 1,
                machine_id INTEGER,
                updated_at TEXT NOT NULL,
                FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE,
                FOREIGN KEY(machine_id) REFERENCES machines(id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS movements (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                swab_id INTEGER NOT NULL,
                action TEXT NOT NULL CHECK(action IN ('TAKE','RETURN')),
                machine_id INTEGER,
                ts TEXT NOT NULL,
                note TEXT,
                FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE,
                FOREIGN KEY(machine_id) REFERENCES machines(id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS idx_movements_swab_action_ts
              ON movements(swab_id, action, ts);

            CREATE TABLE IF NOT EXISTS usage_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                swab_id INTEGER NOT NULL,
                taken_ts TEXT NOT NULL,
                returned_ts TEXT,
                FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_usage_sessions_open ON usage_sessions(swab_id, returned_ts);

            -- Giorni unici di utilizzo (se prendo/reso 10 volte nello stesso giorno => 1 solo)
            CREATE TABLE IF NOT EXISTS usage_days (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                swab_id INTEGER NOT NULL,
                day TEXT NOT NULL,
                FOREIGN KEY(swab_id) REFERENCES swabs(id) ON DELETE CASCADE,
                UNIQUE(swab_id, day)
            );

            CREATE INDEX IF NOT EXISTS idx_usage_days_swab ON usage_days(swab_id);

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """
        )
        con.execute(
            "INSERT OR IGNORE INTO settings (key, value) VALUES (?, ?)",
            (SETTINGS_KEY_WARN_DAYS, str(DEFAULT_GLOBAL_WARN_DAYS)),
        )
        con.execute(
            "INSERT OR IGNORE INTO settings (key, value) VALUES (?, ?)",
            (SETTINGS_KEY_ALARM_DAYS, str(DEFAULT_GLOBAL_ALARM_DAYS)),
        )
        con.execute(
            "INSERT OR IGNORE INTO settings (key, value) VALUES (?, ?)",
            (SETTINGS_KEY_BARCODE_MODULE_WIDTH, str(DEFAULT_BARCODE_MODULE_WIDTH)),
        )
        con.execute(
            "INSERT OR IGNORE INTO settings (key, value) VALUES (?, ?)",
            (SETTINGS_KEY_BARCODE_MODULE_HEIGHT, str(DEFAULT_BARCODE_MODULE_HEIGHT)),
        )
        con.execute(
            "INSERT OR IGNORE INTO settings (key, value) VALUES (?, ?)",
            (SETTINGS_KEY_BARCODE_QUIET_ZONE, str(DEFAULT_BARCODE_QUIET_ZONE)),
        )
        con.execute(
            "INSERT OR IGNORE INTO settings (key, value) VALUES (?, ?)",
            (SETTINGS_KEY_BARCODE_FONT_SIZE, str(DEFAULT_BARCODE_FONT_SIZE)),
        )
        con.execute(
            "INSERT OR IGNORE INTO settings (key, value) VALUES (?, ?)",
            (SETTINGS_KEY_BARCODE_TEXT_DISTANCE, str(DEFAULT_BARCODE_TEXT_DISTANCE)),
        )
        con.execute(
            "INSERT OR IGNORE INTO settings (key, value) VALUES (?, ?)",
            (SETTINGS_KEY_BARCODE_WRITE_TEXT, "1" if DEFAULT_BARCODE_WRITE_TEXT else "0"),
        )
        con.execute(
            "INSERT OR IGNORE INTO settings (key, value) VALUES (?, ?)",
            (
                SETTINGS_KEY_BARCODE_SETTINGS_HASH,
                compute_barcode_settings_hash({
                    "module_width": DEFAULT_BARCODE_MODULE_WIDTH,
                    "module_height": DEFAULT_BARCODE_MODULE_HEIGHT,
                    "quiet_zone": DEFAULT_BARCODE_QUIET_ZONE,
                    "font_size": DEFAULT_BARCODE_FONT_SIZE,
                    "text_distance": DEFAULT_BARCODE_TEXT_DISTANCE,
                    "write_text": DEFAULT_BARCODE_WRITE_TEXT,
                }),
            ),
        )
        con.commit()


def get_setting(con: sqlite3.Connection, key: str) -> Optional[str]:
    row = con.execute("SELECT value FROM settings WHERE key=?", (key,)).fetchone()
    return row["value"] if row else None


def set_setting(con: sqlite3.Connection, key: str, value: str) -> None:
    con.execute(
        "INSERT INTO settings (key, value) VALUES (?, ?) "
        "ON CONFLICT(key) DO UPDATE SET value=excluded.value",
        (key, value),
    )


def get_positive_setting(con: sqlite3.Connection, key: str, default: int) -> int:
    raw = get_setting(con, key)
    try:
        value = int(raw) if raw is not None else default
    except (TypeError, ValueError):
        value = default
    if value <= 0:
        value = default
    return value


def get_positive_float_setting(con: sqlite3.Connection, key: str, default: float) -> float:
    raw = get_setting(con, key)
    try:
        value = float(raw) if raw is not None else default
    except (TypeError, ValueError):
        value = default
    if value <= 0:
        value = default
    return value


def get_boolean_setting(con: sqlite3.Connection, key: str, default: bool) -> bool:
    raw = get_setting(con, key)
    if raw is None:
        return default
    normalized = str(raw).strip().lower()
    if normalized in ("1", "true", "yes", "on"):
        return True
    if normalized in ("0", "false", "no", "off"):
        return False
    return default


def get_global_warn_days(con: sqlite3.Connection) -> int:
    return get_positive_setting(con, SETTINGS_KEY_WARN_DAYS, DEFAULT_GLOBAL_WARN_DAYS)


def get_global_alarm_days(con: sqlite3.Connection) -> int:
    return get_positive_setting(con, SETTINGS_KEY_ALARM_DAYS, DEFAULT_GLOBAL_ALARM_DAYS)


def get_barcode_settings(con: sqlite3.Connection) -> Dict[str, Any]:
    return {
        "module_width": get_positive_float_setting(
            con,
            SETTINGS_KEY_BARCODE_MODULE_WIDTH,
            DEFAULT_BARCODE_MODULE_WIDTH,
        ),
        "module_height": get_positive_float_setting(
            con,
            SETTINGS_KEY_BARCODE_MODULE_HEIGHT,
            DEFAULT_BARCODE_MODULE_HEIGHT,
        ),
        "quiet_zone": get_positive_float_setting(
            con,
            SETTINGS_KEY_BARCODE_QUIET_ZONE,
            DEFAULT_BARCODE_QUIET_ZONE,
        ),
        "font_size": get_positive_setting(
            con,
            SETTINGS_KEY_BARCODE_FONT_SIZE,
            DEFAULT_BARCODE_FONT_SIZE,
        ),
        "text_distance": get_positive_float_setting(
            con,
            SETTINGS_KEY_BARCODE_TEXT_DISTANCE,
            DEFAULT_BARCODE_TEXT_DISTANCE,
        ),
        "write_text": get_boolean_setting(
            con,
            SETTINGS_KEY_BARCODE_WRITE_TEXT,
            DEFAULT_BARCODE_WRITE_TEXT,
        ),
    }


def get_barcode_settings_hash(con: sqlite3.Connection) -> str:
    current = get_setting(con, SETTINGS_KEY_BARCODE_SETTINGS_HASH)
    if current:
        return current
    computed = compute_barcode_settings_hash(get_barcode_settings(con))
    set_setting(con, SETTINGS_KEY_BARCODE_SETTINGS_HASH, computed)
    return computed


def get_swab_by_sku(con: sqlite3.Connection, sku: str) -> Optional[sqlite3.Row]:
    return con.execute("SELECT * FROM swabs WHERE sku=?", (sku,)).fetchone()


def get_swab_by_id(con: sqlite3.Connection, swab_id: int) -> Optional[sqlite3.Row]:
    return con.execute("SELECT * FROM swabs WHERE id=?", (swab_id,)).fetchone()


def get_state(con: sqlite3.Connection, swab_id: int) -> sqlite3.Row:
    row = con.execute(
        "SELECT in_stock, machine_id, updated_at FROM swab_state WHERE swab_id=?",
        (swab_id,),
    ).fetchone()
    if row:
        return row
    return {"in_stock": 1, "machine_id": None, "updated_at": None}


def set_state(con: sqlite3.Connection, swab_id: int, in_stock: int, machine_id: Optional[int]) -> None:
    if int(in_stock) == 1:
        machine_id = None
    con.execute(
        "INSERT OR REPLACE INTO swab_state (swab_id, in_stock, machine_id, updated_at) VALUES (?, ?, ?, ?)",
        (swab_id, int(in_stock), machine_id, now_iso()),
    )


def ensure_label_png(sku: str) -> str:
    ensure_dir(LABELS_DIR)
    out_path = os.path.join(LABELS_DIR, f"{sku}.png")
    hash_path = os.path.join(LABELS_DIR, f"{sku}.hash")

    with connect() as con:
        barcode_settings = get_barcode_settings(con)
        settings_hash = get_barcode_settings_hash(con)

    current_hash = None
    if os.path.exists(hash_path):
        with open(hash_path, "r", encoding="utf-8") as handle:
            current_hash = handle.read().strip() or None

    if os.path.exists(out_path) and current_hash == settings_hash:
        return out_path

    if os.path.exists(out_path):
        os.remove(out_path)

    writer = ImageWriter()
    code = Code128(sku, writer=writer)
    code.save(
        os.path.join(LABELS_DIR, sku),
        options=barcode_settings,
    )
    with open(hash_path, "w", encoding="utf-8") as handle:
        handle.write(settings_hash)
    return out_path


def open_taken_ts(con: sqlite3.Connection, swab_id: int) -> Optional[str]:
    row = con.execute(
        "SELECT taken_ts FROM usage_sessions WHERE swab_id=? AND returned_ts IS NULL ORDER BY taken_ts DESC LIMIT 1",
        (swab_id,),
    ).fetchone()
    return row["taken_ts"] if row else None


def total_unique_days(con: sqlite3.Connection, swab_id: int) -> int:
    row = con.execute("SELECT COUNT(*) AS c FROM usage_days WHERE swab_id=?", (swab_id,)).fetchone()
    return int(row["c"]) if row else 0


def add_usage_days_for_range(con: sqlite3.Connection, swab_id: int, start_iso: str, end_iso: str) -> int:
    a = parse_iso(start_iso).date()
    b = parse_iso(end_iso).date()
    before = con.total_changes
    for d in iter_dates_inclusive(a, b):
        con.execute(
            "INSERT OR IGNORE INTO usage_days (swab_id, day) VALUES (?, ?)",
            (swab_id, date_to_key(d)),
        )
    after = con.total_changes
    return max(0, after - before)


def list_machines(con: sqlite3.Connection) -> List[Dict[str, Any]]:
    rows = con.execute("SELECT id, name FROM machines ORDER BY name COLLATE NOCASE").fetchall()
    return [{"id": int(r["id"]), "name": r["name"]} for r in rows]


def machine_exists(con: sqlite3.Connection, machine_id: int) -> bool:
    r = con.execute("SELECT 1 FROM machines WHERE id=?", (machine_id,)).fetchone()
    return bool(r)


# ---------------------------
# Routes
# ---------------------------
@app.route("/")
def home():
    return redirect(url_for("swabs"))


# --- Public pages ---
def fetch_swabs(query: str) -> List[Dict[str, Any]]:
    like_query = f"%{query}%"
    with connect() as con:
        warn_days = get_global_warn_days(con)
        alarm_days = get_global_alarm_days(con)
        sql = """
            SELECT s.id, s.sku, s.name,
                   COALESCE(st.in_stock, 1) AS in_stock,
                   COALESCE(st.updated_at, s.created_at) AS updated_at,
                   st.machine_id AS machine_id,
                   mc.name AS machine_name,

                   (SELECT mv.ts FROM movements mv WHERE mv.swab_id=s.id AND mv.action='TAKE' ORDER BY mv.ts DESC LIMIT 1) AS last_take_ts,
                   (SELECT mv.ts FROM movements mv WHERE mv.swab_id=s.id AND mv.action='RETURN' ORDER BY mv.ts DESC LIMIT 1) AS last_return_ts
            FROM swabs s
            LEFT JOIN swab_state st ON st.swab_id = s.id
            LEFT JOIN machines mc ON mc.id = st.machine_id
        """
        params: Tuple[Any, ...] = ()
        if query:
            sql += """
            WHERE s.name LIKE ? COLLATE NOCASE
               OR mc.name LIKE ? COLLATE NOCASE
            """
            params = (like_query, like_query)
        sql += "ORDER BY s.name COLLATE NOCASE"

        rows = con.execute(sql, params).fetchall()

        enriched: List[Dict[str, Any]] = []
        for r in rows:
            swab_id = int(r["id"])
            ot = open_taken_ts(con, swab_id)
            current_days = current_calendar_days(ot) if ot else 0
            total_days = total_unique_days(con, swab_id)
            is_warning = current_days > warn_days or total_days > warn_days
            is_alarm = current_days > alarm_days or total_days > alarm_days

            enriched.append({
                "id": swab_id,
                "sku": r["sku"],
                "name": r["name"],
                "in_stock": int(r["in_stock"]),
                "updated_at": r["updated_at"],
                "open_taken_ts": ot,
                "current_days": current_days,
                "total_days": total_days,
                "warning": is_warning,
                "alarm": is_alarm,
                "last_take_ts": r["last_take_ts"],
                "last_return_ts": r["last_return_ts"],
                "machine_name": r["machine_name"],
            })
    return enriched


@app.route("/swabs", methods=["GET"])
def swabs():
    init_db()

    # GET pubblico
    query = (request.args.get("q") or "").strip()
    enriched = fetch_swabs(query)
    with connect() as con:
        warn_days = get_global_warn_days(con)
        alarm_days = get_global_alarm_days(con)

    return render_template(
        "swabs.html",
        rows=enriched,
        global_warn_days=warn_days,
        global_alarm_days=alarm_days,
        q=query,
    )


@app.route("/admin")
@require_admin
def admin_dashboard():
    return render_template("admin_dashboard.html")


@app.route("/admin/settings", methods=["GET", "POST"])
@require_admin
def admin_settings():
    init_db()
    with connect() as con:
        if request.method == "POST":
            raw_warn = (request.form.get("global_warn_days") or "").strip()
            raw_alarm = (request.form.get("global_alarm_days") or "").strip()
            raw_module_width = (request.form.get("barcode_module_width") or "").strip()
            raw_module_height = (request.form.get("barcode_module_height") or "").strip()
            raw_quiet_zone = (request.form.get("barcode_quiet_zone") or "").strip()
            raw_font_size = (request.form.get("barcode_font_size") or "").strip()
            raw_text_distance = (request.form.get("barcode_text_distance") or "").strip()
            write_text_values = request.form.getlist("barcode_write_text")
            raw_write_text = (write_text_values[-1] if write_text_values else "0").strip()
            try:
                warn_value = int(raw_warn)
                alarm_value = int(raw_alarm)
                if warn_value <= 0 or alarm_value <= 0:
                    raise ValueError("Valore non valido")
                if warn_value >= alarm_value:
                    raise ValueError("Soglie non valide")
                module_width = float(raw_module_width)
                module_height = float(raw_module_height)
                quiet_zone = float(raw_quiet_zone)
                font_size = int(raw_font_size)
                text_distance = float(raw_text_distance)
                if (
                    module_width <= 0
                    or module_height <= 0
                    or quiet_zone <= 0
                    or font_size <= 0
                    or text_distance <= 0
                ):
                    raise ValueError("Parametri barcode non validi")
                if raw_write_text not in ("0", "1"):
                    raise ValueError("Parametro testo barcode non valido")
            except ValueError:
                flash(
                    "Inserisci soglie valide (interi positivi, avviso < allarme) e parametri barcode corretti.",
                    "error",
                )
                return redirect(url_for("admin_settings"))

            set_setting(con, SETTINGS_KEY_WARN_DAYS, str(warn_value))
            set_setting(con, SETTINGS_KEY_ALARM_DAYS, str(alarm_value))
            set_setting(con, SETTINGS_KEY_BARCODE_MODULE_WIDTH, str(module_width))
            set_setting(con, SETTINGS_KEY_BARCODE_MODULE_HEIGHT, str(module_height))
            set_setting(con, SETTINGS_KEY_BARCODE_QUIET_ZONE, str(quiet_zone))
            set_setting(con, SETTINGS_KEY_BARCODE_FONT_SIZE, str(font_size))
            set_setting(con, SETTINGS_KEY_BARCODE_TEXT_DISTANCE, str(text_distance))
            set_setting(con, SETTINGS_KEY_BARCODE_WRITE_TEXT, raw_write_text)
            barcode_hash = compute_barcode_settings_hash({
                "module_width": module_width,
                "module_height": module_height,
                "quiet_zone": quiet_zone,
                "font_size": font_size,
                "text_distance": text_distance,
                "write_text": raw_write_text == "1",
            })
            set_setting(con, SETTINGS_KEY_BARCODE_SETTINGS_HASH, barcode_hash)
            con.commit()
            flash("Impostazioni aggiornate.", "ok")
            return redirect(url_for("admin_settings"))

        warn_days = get_global_warn_days(con)
        alarm_days = get_global_alarm_days(con)
        barcode_settings = get_barcode_settings(con)
    return render_template(
        "admin_settings.html",
        global_warn_days=warn_days,
        global_alarm_days=alarm_days,
        barcode_settings=barcode_settings,
    )


@app.route("/admin/swabs", methods=["GET", "POST"])
@require_admin
def admin_swabs():
    init_db()

    if request.method == "POST":
        action = request.form.get("action", "")
        sku = (request.form.get("sku") or "").strip()
        name = (request.form.get("name") or "").strip()

        if action != "add":
            flash("Azione non valida.", "error")
            return redirect(url_for("admin_swabs"))

        with connect() as con:
            if not sku or not name:
                flash("SKU e Nome tampone sono obbligatori.", "error")
                return redirect(url_for("admin_swabs"))

            ts = now_iso()
            try:
                cur = con.execute(
                    "INSERT INTO swabs (sku, name, created_at) VALUES (?, ?, ?)",
                    (sku, name, ts),
                )
                swab_id = cur.lastrowid
                set_state(con, swab_id, 1, None)  # RESO, magazzino
                con.commit()
                ensure_label_png(sku)
                flash(f"Tampone aggiunto: {sku}", "ok")
            except sqlite3.IntegrityError:
                flash(f"SKU già esistente: {sku}", "error")

        return redirect(url_for("admin_swabs"))

    query = (request.args.get("q") or "").strip()
    enriched = fetch_swabs(query)
    with connect() as con:
        warn_days = get_global_warn_days(con)
        alarm_days = get_global_alarm_days(con)
    return render_template(
        "admin_swabs.html",
        rows=enriched,
        global_warn_days=warn_days,
        global_alarm_days=alarm_days,
        q=query,
    )


@app.route("/history")
def history():
    init_db()
    limit = int(request.args.get("limit", "150"))
    limit = max(1, min(limit, 500))

    with connect() as con:
        rows = con.execute(
            """
            SELECT mv.ts, mv.action,
                   sw.sku, sw.name,
                   COALESCE(mv.note,'') AS note,
                   mc.name AS machine_name
            FROM movements mv
            JOIN swabs sw ON sw.id = mv.swab_id
            LEFT JOIN machines mc ON mc.id = mv.machine_id
            ORDER BY mv.ts DESC
            LIMIT ?
            """,
            (limit,),
        ).fetchall()

    return render_template("history.html", rows=rows, limit=limit)


# --- Protected swab edit/delete ---
@app.route("/swabs/<int:swab_id>/edit", methods=["GET", "POST"])
@require_admin
def swab_edit(swab_id: int):
    init_db()
    with connect() as con:
        sw = get_swab_by_id(con, swab_id)
        if not sw:
            flash("Tampone non trovato.", "error")
            return redirect(url_for("admin_swabs"))

        if request.method == "POST":
            new_name = (request.form.get("name") or "").strip()
            new_sku = (request.form.get("sku") or "").strip()

            if not new_name or not new_sku:
                flash("Nome e SKU sono obbligatori.", "error")
                return redirect(url_for("swab_edit", swab_id=swab_id))

            try:
                con.execute("UPDATE swabs SET name=?, sku=? WHERE id=?", (new_name, new_sku, swab_id))
                con.commit()
                ensure_label_png(new_sku)
                flash("Tampone aggiornato.", "ok")
                return redirect(url_for("admin_swabs"))
            except sqlite3.IntegrityError:
                flash("SKU già esistente su un altro tampone.", "error")

    return render_template("swab_edit.html", swab=sw)


@app.route("/swabs/<int:swab_id>/delete", methods=["POST"])
@require_admin
def swab_delete(swab_id: int):
    init_db()
    with connect() as con:
        sw = get_swab_by_id(con, swab_id)
        if not sw:
            flash("Tampone non trovato.", "error")
            return redirect(url_for("admin_swabs"))

        st = get_state(con, swab_id)
        if int(st["in_stock"]) == 0:
            flash("Non puoi eliminare un tampone che risulta PRESO. Rendilo prima.", "error")
            return redirect(url_for("admin_swabs"))

        con.execute("DELETE FROM swabs WHERE id=?", (swab_id,))
        con.commit()

    flash("Tampone eliminato.", "ok")
    return redirect(url_for("admin_swabs"))


# --- Machines page redirect (legacy) ---
@app.route("/machines")
def machines_redirect():
    return redirect(url_for("admin_machines"))


# --- Admin machines ---
@app.route("/admin/machines", methods=["GET", "POST"])
@require_admin
def admin_machines():
    init_db()

    if request.method == "POST":
        action = request.form.get("action", "")
        name = (request.form.get("name") or "").strip()
        mid = (request.form.get("id") or "").strip()

        with connect() as con:
            if action == "add":
                if not name:
                    flash("Nome macchina obbligatorio.", "error")
                    return redirect(url_for("admin_machines"))
                try:
                    con.execute("INSERT INTO machines (name) VALUES (?)", (name,))
                    con.commit()
                    flash(f"Macchina aggiunta: {name}", "ok")
                except sqlite3.IntegrityError:
                    flash("Macchina già esistente.", "error")

            elif action == "delete":
                try:
                    machine_id = int(mid)
                except Exception:
                    flash("ID macchina non valido.", "error")
                    return redirect(url_for("admin_machines"))

                in_use = con.execute(
                    "SELECT 1 FROM swab_state WHERE machine_id=? LIMIT 1",
                    (machine_id,),
                ).fetchone()
                if in_use:
                    flash("Non puoi eliminare: macchina attualmente associata a un tampone PRESO.", "error")
                else:
                    con.execute("DELETE FROM machines WHERE id=?", (machine_id,))
                    con.commit()
                    flash("Macchina eliminata.", "ok")
            else:
                flash("Azione non valida.", "error")

        return redirect(url_for("admin_machines"))

    with connect() as con:
        ms = list_machines(con)
    return render_template("admin_machines.html", machines=ms)


# --- Scanning pages (public) ---
@app.route("/scan")
def scan():
    init_db()
    with connect() as con:
        warn_days = get_global_warn_days(con)
        alarm_days = get_global_alarm_days(con)
    return render_template(
        "scan.html",
        global_warn_days=warn_days,
        global_alarm_days=alarm_days,
    )


@app.route("/scan-camera")
def scan_camera():
    init_db()
    with connect() as con:
        warn_days = get_global_warn_days(con)
        alarm_days = get_global_alarm_days(con)
    return render_template(
        "scan_camera.html",
        global_warn_days=warn_days,
        global_alarm_days=alarm_days,
    )


@app.route("/api/machines")
def api_machines():
    init_db()
    with connect() as con:
        return jsonify({"ok": True, "machines": list_machines(con)})


@app.route("/api/scan", methods=["POST"])
def api_scan():
    """
    JSON:
      { sku: "...", mode: "TOGGLE"|"TAKE"|"RETURN", machine_id?: number }
    - Se l'azione risultante è TAKE e machine_id non c'è -> 409 need_machine con lista macchine
    - Su RETURN ignora machine_id e svuota la macchina (magazzino)
    """
    init_db()
    data: Dict[str, Any] = request.get_json(force=True) or {}
    sku = (data.get("sku") or "").strip()
    mode = (data.get("mode") or "TOGGLE").upper()
    machine_id = data.get("machine_id", None)

    if not sku:
        return jsonify({"ok": False, "error": "SKU vuoto"}), 400
    if mode not in ("TOGGLE", "TAKE", "RETURN"):
        return jsonify({"ok": False, "error": "mode non valido"}), 400

    with connect() as con:
        sw = get_swab_by_sku(con, sku)
        if not sw:
            return jsonify({"ok": False, "error": f"SKU non trovato: {sku}"}), 404

        swab_id = int(sw["id"])
        state = get_state(con, swab_id)
        current_in_stock = int(state["in_stock"])  # 1=RESO, 0=PRESO

        if mode == "TAKE":
            action, new_in_stock = "TAKE", 0
        elif mode == "RETURN":
            action, new_in_stock = "RETURN", 1
        else:
            action, new_in_stock = ("TAKE", 0) if current_in_stock == 1 else ("RETURN", 1)

        # TAKE: richiede macchina
        if action == "TAKE":
            try:
                mid_int = int(machine_id) if machine_id is not None else None
            except Exception:
                mid_int = None

            if not mid_int:
                return jsonify({
                    "ok": False,
                    "need_machine": True,
                    "message": "Seleziona la macchina per registrare il PRESO.",
                    "machines": list_machines(con),
                    "sku": sku,
                    "mode": mode
                }), 409

            if not machine_exists(con, mid_int):
                return jsonify({"ok": False, "error": "Macchina non valida"}), 400

        ts = now_iso()
        days_session = None
        added_unique_days = 0

        if action == "TAKE":
            con.execute(
                "INSERT INTO movements (swab_id, action, machine_id, ts, note) VALUES (?, 'TAKE', ?, ?, NULL)",
                (swab_id, int(machine_id), ts),
            )

            # apre sessione se non esiste già aperta
            open_sess = con.execute(
                "SELECT id FROM usage_sessions WHERE swab_id=? AND returned_ts IS NULL ORDER BY taken_ts DESC LIMIT 1",
                (swab_id,),
            ).fetchone()
            if not open_sess:
                con.execute(
                    "INSERT INTO usage_sessions (swab_id, taken_ts, returned_ts) VALUES (?, ?, NULL)",
                    (swab_id, ts),
                )

            # stato: preso + macchina
            set_state(con, swab_id, 0, int(machine_id))

        else:
            con.execute(
                "INSERT INTO movements (swab_id, action, machine_id, ts, note) VALUES (?, 'RETURN', NULL, ?, NULL)",
                (swab_id, ts),
            )

            sess = con.execute(
                "SELECT id, taken_ts FROM usage_sessions WHERE swab_id=? AND returned_ts IS NULL ORDER BY taken_ts DESC LIMIT 1",
                (swab_id,),
            ).fetchone()
            if sess:
                taken_ts = sess["taken_ts"]
                days_session = calendar_days_between(taken_ts, ts)
                con.execute("UPDATE usage_sessions SET returned_ts=? WHERE id=?", (ts, sess["id"]))
                added_unique_days = add_usage_days_for_range(con, swab_id, taken_ts, ts)

            # stato: reso => macchina NULL
            set_state(con, swab_id, 1, None)

        ot = open_taken_ts(con, swab_id)
        current_days = current_calendar_days(ot) if ot else 0
        total_days = total_unique_days(con, swab_id)
        warn_days = get_global_warn_days(con)
        alarm_days = get_global_alarm_days(con)
        is_warning = current_days > warn_days or total_days > warn_days
        is_alarm = current_days > alarm_days or total_days > alarm_days

        # macchina corrente (solo se preso)
        st2 = get_state(con, swab_id)
        machine_name = None
        if st2["machine_id"]:
            m = con.execute("SELECT name FROM machines WHERE id=?", (int(st2["machine_id"]),)).fetchone()
            machine_name = m["name"] if m else None

        con.commit()

        return jsonify({
            "ok": True,
            "sku": sku,
            "name": sw["name"],
            "action": action,
            "in_stock": bool(new_in_stock == 1),
            "machine_name": machine_name,
            "ts": ts,
            "days_session": days_session,
            "added_unique_days": added_unique_days,
            "current_days": current_days,
            "total_days": total_days,
            "warn_days": warn_days,
            "alarm_days": alarm_days,
            "warning": is_warning,
            "alarm": is_alarm,
        })


@app.route("/label/<sku>.png")
def label_png(sku: str):
    init_db()
    sku = (sku or "").strip()
    if not sku:
        return "SKU non valido", 400

    with connect() as con:
        sw = get_swab_by_sku(con, sku)
        if not sw:
            return "SKU non trovato", 404

    path = ensure_label_png(sku)
    return send_file(path, mimetype="image/png", as_attachment=False)


@app.route("/label/<sku>/print")
def label_print(sku: str):
    init_db()
    sku = (sku or "").strip()
    if not sku:
        return "SKU non valido", 400

    with connect() as con:
        sw = get_swab_by_sku(con, sku)
        if not sw:
            return "SKU non trovato", 404

    ensure_label_png(sku)
    return render_template(
        "label_print.html",
        sku=sku,
        swab_name=sw["name"],
        label_url=url_for("label_png", sku=sku),
    )


@app.route("/labels/print", methods=["GET", "POST"])
def labels_print():
    init_db()
    raw_skus = request.values.getlist("selected_skus")
    selected_skus = [sku.strip() for sku in raw_skus if sku and sku.strip()]
    if not selected_skus:
        return "Nessuno SKU selezionato", 400

    labels: List[Dict[str, str]] = []
    with connect() as con:
        for sku in selected_skus:
            if not sku:
                return "SKU non valido", 400
            sw = get_swab_by_sku(con, sku)
            if not sw:
                return f"SKU non trovato: {sku}", 404
            ensure_label_png(sku)
            labels.append({
                "sku": sku,
                "name": sw["name"],
                "label_url": url_for("label_png", sku=sku),
            })

    return render_template("labels_print.html", labels=labels)


if __name__ == "__main__":
    import threading
    import webbrowser

    init_db()
    ensure_dir(LABELS_DIR)
    # accessibile da LAN: host="0.0.0.0" (se vuoi)
    threading.Timer(1.5, lambda: webbrowser.open(START_URL, new=2)).start()
    app.run(host="0.0.0.0", port=8086, debug=False)
'''    
if __name__ == "__main__":
    import threading
    import webbrowser

    init_db()
    ensure_dir(LABELS_DIR)
    # accessibile da LAN: host="0.0.0.0" (se vuoi)
    threading.Timer(1.5, lambda: webbrowser.open(START_URL, new=2)).start()
    app.run(host="0.0.0.0", port=8086, debug=False, ssl_context=(ssl_cert, ssl_file))
'''
