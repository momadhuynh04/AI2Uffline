"""
 Ultimate Fix - Configurator
A modern dark-themed GUI for configuring the AI2U game mod.
"""

import tkinter as tk
from tkinter import ttk, messagebox, font as tkfont
import json
import os
import sys

# ── Paths ──
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
CONFIG_PATH = os.path.join(SCRIPT_DIR, "config", "Config.json")

# ── Default Values ──
DEFAULT_SYSTEM_PROMPT = """You are an AI controlling an NPC . You must ALWAYS respond in valid JSON format.

Your response MUST be a JSON object with this exact structure:
{
  "npc_reactions": {
    "npc_reply_to_player": "Your dialogue text here",
    "npc_body_animation": "idle",
    "npc_face_expression": "smile",
    "npc_emotion_type": "happy",
    "npc_emotion_score": "5",
    "angry_level": "0",
    "favorability_change": "0",
    "npc_action": "standing",
    "npc_target_location": "",
    "giving_to_player": "",
    "character": 0
  },
  "completion": 0,
  "total": 0
}

Animation options: idle, walk, run, sit, laugh, cry, angry, dance, wave
Face expressions: smile, sad, angry, surprised, neutral, disgusted, scared, happy
Emotion types: happy, sad, angry, scared, surprised, disgusted, neutral, love, jealous
Emotion score: 0-10 (intensity)
Angry level: 0-10
Favorability change: -5 to 5
npc_action: standing, walking, running, sitting
npc_target_location: living_room, bedroom, kitchen, bathroom, closet, balcony, entrance

IMPORTANT: Only output the JSON object. No additional text, no markdown, no code fences.""".strip()

DEFAULT_POST_HISTORY = "Remember: You MUST respond ONLY with a valid JSON object following the npc_reactions format. No other text, no markdown."

DEFAULTS = {
    "base_url": "https://openrouter.ai/api/v1/chat/completions",
    "api_key": "",
    "model": "openai/gpt-4o-mini",
    "system_prompt": DEFAULT_SYSTEM_PROMPT,
    "post_history_prompt": DEFAULT_POST_HISTORY,
    "temperature": 0.7,
    "top_p": 0.95,
    "top_k": 0,
    "max_tokens": 800,
    "frequency_penalty": 0.03,
    "presence_penalty": 0.03,
    "tts_enable": False,
    "tts_provider": "Azure",
    "tts_base_url": "",
    "tts_api_key": "",
    "tts_model": "en-US-JaneNeural",
    "tts_region": "eastus",
}

# ── Colors ──
BG           = "#1a1a2e"
BG_CARD      = "#25254a"
BG_INPUT     = "#2d2d55"
BG_HOVER     = "#35356a"
ACCENT       = "#7c3aed"
ACCENT_HOVER = "#9055ff"
TEXT         = "#e8e8f0"
TEXT_DIM     = "#9898b8"
TEXT_LABEL   = "#c0c0d8"
BORDER       = "#3a3a6a"
SUCCESS      = "#22c55e"
WARNING      = "#f59e0b"
ERROR        = "#ef4444"


class ToolTip:
    """Simple tooltip for widgets."""
    def __init__(self, widget, text):
        self.widget = widget
        self.text = text
        self.tipwindow = None
        widget.bind("<Enter>", self.show)
        widget.bind("<Leave>", self.hide)

    def show(self, event=None):
        if self.tipwindow:
            return
        x = self.widget.winfo_rootx() + 20
        y = self.widget.winfo_rooty() + self.widget.winfo_height() + 5
        self.tipwindow = tw = tk.Toplevel(self.widget)
        tw.wm_overrideredirect(True)
        tw.wm_geometry(f"+{x}+{y}")
        tw.configure(bg="#2a2a4a")
        label = tk.Label(tw, text=self.text, justify="left",
                         bg="#2a2a4a", fg=TEXT_DIM,
                         font=("Segoe UI", 9), padx=8, pady=4,
                         wraplength=350)
        label.pack()

    def hide(self, event=None):
        if self.tipwindow:
            self.tipwindow.destroy()
            self.tipwindow = None


class AI2UConfigurator:
    def __init__(self, root):
        self.root = root
        self.root.title("AI2U Ultimate Fix - Configurator")
        self.root.geometry("820x920")
        self.root.minsize(700, 700)
        self.root.configure(bg=BG)

        # Try to set icon
        try:
            self.root.iconbitmap(default="")
        except:
            pass

        self.show_key = tk.BooleanVar(value=False)
        self.show_tts_key = tk.BooleanVar(value=False)
        self.status_var = tk.StringVar(value="")
        self.config = dict(DEFAULTS)

        self._build_styles()
        self._build_ui()
        self._load_config()

    def _build_styles(self):
        style = ttk.Style()
        style.theme_use("clam")

        style.configure(".", background=BG, foreground=TEXT, fieldbackground=BG_INPUT)
        style.configure("TFrame", background=BG)
        style.configure("Card.TFrame", background=BG_CARD)
        style.configure("TLabel", background=BG, foreground=TEXT_LABEL, font=("Segoe UI", 10))
        style.configure("Title.TLabel", background=BG, foreground=TEXT, font=("Segoe UI", 22, "bold"))
        style.configure("Subtitle.TLabel", background=BG, foreground=TEXT_DIM, font=("Segoe UI", 10))
        style.configure("Section.TLabel", background=BG_CARD, foreground=ACCENT, font=("Segoe UI", 12, "bold"))
        style.configure("Value.TLabel", background=BG_CARD, foreground=ACCENT, font=("Segoe UI Semibold", 10))
        style.configure("Status.TLabel", background=BG, foreground=SUCCESS, font=("Segoe UI", 10))

        style.configure("TLabelframe", background=BG_CARD, foreground=ACCENT,
                         font=("Segoe UI", 11, "bold"), borderwidth=1, relief="solid")
        style.configure("TLabelframe.Label", background=BG_CARD, foreground=ACCENT,
                         font=("Segoe UI", 11, "bold"))

        style.configure("Accent.TButton", background=ACCENT, foreground="white",
                         font=("Segoe UI Semibold", 11), padding=(20, 8))
        style.map("Accent.TButton",
                  background=[("active", ACCENT_HOVER), ("pressed", ACCENT)])

        style.configure("Secondary.TButton", background=BG_INPUT, foreground=TEXT,
                         font=("Segoe UI", 10), padding=(12, 6))
        style.map("Secondary.TButton",
                  background=[("active", BG_HOVER)])

        style.configure("Small.TButton", background=BG_INPUT, foreground=TEXT_DIM,
                         font=("Segoe UI", 9), padding=(6, 3))
        style.map("Small.TButton", background=[("active", BG_HOVER)])

        style.configure("Horizontal.TScale", background=BG_CARD, troughcolor=BG_INPUT,
                         sliderthickness=16)

    def _build_ui(self):
        # ── Scrollable container ──
        outer = tk.Frame(self.root, bg=BG)
        outer.pack(fill="both", expand=True)

        canvas = tk.Canvas(outer, bg=BG, highlightthickness=0)
        scrollbar = ttk.Scrollbar(outer, orient="vertical", command=canvas.yview)
        self.scroll_frame = tk.Frame(canvas, bg=BG)

        self.scroll_frame.bind("<Configure>",
            lambda e: canvas.configure(scrollregion=canvas.bbox("all")))

        canvas.create_window((0, 0), window=self.scroll_frame, anchor="nw")
        canvas.configure(yscrollcommand=scrollbar.set)

        canvas.pack(side="left", fill="both", expand=True, padx=(15, 0))
        scrollbar.pack(side="right", fill="y")

        # Mouse wheel scroll
        def _on_mousewheel(event):
            canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
        canvas.bind_all("<MouseWheel>", _on_mousewheel)
        self.canvas = canvas

        container = self.scroll_frame
        pad = {"padx": 15, "pady": 5}

        # ── Header ──
        header = tk.Frame(container, bg=BG)
        header.pack(fill="x", padx=15, pady=(15, 5))

        tk.Label(header, text="🎮 AI2U Ultimate Fix", bg=BG, fg=TEXT,
                 font=("Segoe UI", 22, "bold")).pack(side="left")
        tk.Label(header, text="v8.0 Configurator", bg=BG, fg=TEXT_DIM,
                 font=("Segoe UI", 11)).pack(side="left", padx=(10, 0), pady=(8, 0))

        # ── API Settings ──
        self._build_api_section(container, pad)

        # ── TTS Settings ──
        self._build_tts_section(container, pad)

        # ── AI Parameters ──
        self._build_params_section(container, pad)

        # ── System Prompt ──
        self._build_prompt_section(container, pad, "System Prompt",
            "system_prompt", "Tells the AI how to behave and respond. Include JSON format instructions here.",
            height=12)

        # ── Post-History Prompt ──
        self._build_prompt_section(container, pad, "Post-History Prompt",
            "post_history_prompt", "Appended after chat history to remind the AI of output format.",
            height=4)

        # ── Buttons ──
        btn_frame = tk.Frame(container, bg=BG)
        btn_frame.pack(fill="x", padx=15, pady=(10, 5))

        save_btn = tk.Button(btn_frame, text="💾  Save Configuration", bg=ACCENT, fg="white",
                             font=("Segoe UI Semibold", 12), relief="flat", cursor="hand2",
                             activebackground=ACCENT_HOVER, activeforeground="white",
                             padx=30, pady=10, command=self._save_config)
        save_btn.pack(side="left", padx=(0, 8))

        load_btn = tk.Button(btn_frame, text="📂  Reload", bg=BG_INPUT, fg=TEXT,
                             font=("Segoe UI", 10), relief="flat", cursor="hand2",
                             activebackground=BG_HOVER, padx=15, pady=10,
                             command=self._load_config)
        load_btn.pack(side="left", padx=(0, 8))

        reset_btn = tk.Button(btn_frame, text="🔄  Reset Defaults", bg=BG_INPUT, fg=TEXT_DIM,
                              font=("Segoe UI", 10), relief="flat", cursor="hand2",
                              activebackground=BG_HOVER, padx=15, pady=10,
                              command=self._reset_defaults)
        reset_btn.pack(side="left")

        # ── Status bar ──
        status_frame = tk.Frame(container, bg=BG)
        status_frame.pack(fill="x", padx=15, pady=(0, 15))
        self.status_label = tk.Label(status_frame, textvariable=self.status_var,
                                     bg=BG, fg=SUCCESS, font=("Segoe UI", 10))
        self.status_label.pack(side="left")

    def _build_api_section(self, container, pad):
        frame = tk.LabelFrame(container, text="  🔑 API Settings  ", bg=BG_CARD, fg=ACCENT,
                              font=("Segoe UI", 11, "bold"), bd=1, relief="solid",
                              highlightbackground=BORDER, highlightthickness=1)
        frame.pack(fill="x", padx=15, pady=(5, 3))

        inner = tk.Frame(frame, bg=BG_CARD)
        inner.pack(fill="x", padx=15, pady=10)

        # Base URL
        self._make_label(inner, "Base URL", 0, "The API endpoint URL (e.g. OpenRouter, OpenAI)")
        self.base_url_var = tk.StringVar()
        self._make_entry(inner, self.base_url_var, 0)

        # API Key
        self._make_label(inner, "API Key", 1, "Your API key. Keep this secret!")
        key_frame = tk.Frame(inner, bg=BG_CARD)
        key_frame.grid(row=1, column=1, sticky="ew", pady=3)
        self.api_key_var = tk.StringVar()
        self.key_entry = tk.Entry(key_frame, textvariable=self.api_key_var, show="•",
                                  bg=BG_INPUT, fg=TEXT, insertbackground=TEXT,
                                  font=("Consolas", 10), relief="flat", bd=0)
        self.key_entry.pack(side="left", fill="x", expand=True, ipady=6, padx=(0, 5))

        toggle_btn = tk.Button(key_frame, text="👁", bg=BG_INPUT, fg=TEXT_DIM,
                               font=("Segoe UI", 9), relief="flat", cursor="hand2",
                               command=self._toggle_key, width=3)
        toggle_btn.pack(side="right")

        # Model
        self._make_label(inner, "Model", 2, "Model name (e.g. openai/gpt-4o-mini, google/gemini-flash-1.5)")
        self.model_var = tk.StringVar()
        self._make_entry(inner, self.model_var, 2)

        inner.columnconfigure(1, weight=1)

    def _build_tts_section(self, container, pad):
        frame = tk.LabelFrame(container, text="  🎙️ TTS Settings (Voice)  ", bg=BG_CARD, fg=ACCENT,
                              font=("Segoe UI", 11, "bold"), bd=1, relief="solid",
                              highlightbackground=BORDER, highlightthickness=1)
        frame.pack(fill="x", padx=15, pady=(5, 3))

        inner = tk.Frame(frame, bg=BG_CARD)
        inner.pack(fill="x", padx=15, pady=10)

        # Enable TTS
        self._make_label(inner, "Enable Custom TTS", 0, "Bypass the game's offline voice and fetch audio from the API.")
        self.tts_enable_var = tk.BooleanVar()
        chk = tk.Checkbutton(inner, variable=self.tts_enable_var, bg=BG_CARD, fg=TEXT,
                             activebackground=BG_CARD, activeforeground=TEXT,
                             selectcolor=BG_INPUT, relief="flat")
        chk.grid(row=0, column=1, sticky="w", pady=3)

        # Provider
        self._make_label(inner, "TTS Provider", 1, "Azure for official voices, OpenAI Compatible for custom servers.")
        self.tts_provider_var = tk.StringVar()
        prov_combo = ttk.Combobox(inner, textvariable=self.tts_provider_var, values=["Azure", "OpenAI Compatible"],
                                  state="readonly", font=("Consolas", 10))
        prov_combo.grid(row=1, column=1, sticky="ew", pady=3, ipady=3)

        # Base URL
        self._make_label(inner, "TTS Base URL", 2, "Leave blank for default Azure. Set your URL if using OpenAI Compatible.")
        self.tts_base_url_var = tk.StringVar()
        self._make_entry(inner, self.tts_base_url_var, 2)

        # API Key
        self._make_label(inner, "TTS API Key", 3, "API Key for Azure TTS or OpenAI Audio API.")
        key_frame = tk.Frame(inner, bg=BG_CARD)
        key_frame.grid(row=3, column=1, sticky="ew", pady=3)
        self.tts_api_key_var = tk.StringVar()
        self.tts_key_entry = tk.Entry(key_frame, textvariable=self.tts_api_key_var, show="•",
                                      bg=BG_INPUT, fg=TEXT, insertbackground=TEXT,
                                      font=("Consolas", 10), relief="flat", bd=0)
        self.tts_key_entry.pack(side="left", fill="x", expand=True, ipady=6, padx=(0, 5))

        toggle_btn = tk.Button(key_frame, text="👁", bg=BG_INPUT, fg=TEXT_DIM,
                               font=("Segoe UI", 9), relief="flat", cursor="hand2",
                               command=self._toggle_tts_key, width=3)
        toggle_btn.pack(side="right")

        # Model/Voice
        self._make_label(inner, "TTS Voice/Model", 4, "Azure: en-US-JaneNeural, zh-CN-XiaoyiNeural. OpenAI: alloy, echo, etc.")
        self.tts_model_var = tk.StringVar()
        self._make_entry(inner, self.tts_model_var, 4)

        # Region
        self._make_label(inner, "Azure Region", 5, "Required for Azure TTS (e.g. eastus, southeastasia).")
        self.tts_region_var = tk.StringVar()
        self._make_entry(inner, self.tts_region_var, 5)

        inner.columnconfigure(1, weight=1)

    def _build_params_section(self, container, pad):
        frame = tk.LabelFrame(container, text="  ⚙️ AI Parameters  ", bg=BG_CARD, fg=ACCENT,
                              font=("Segoe UI", 11, "bold"), bd=1, relief="solid",
                              highlightbackground=BORDER, highlightthickness=1)
        frame.pack(fill="x", padx=15, pady=(3, 3))

        inner = tk.Frame(frame, bg=BG_CARD)
        inner.pack(fill="x", padx=15, pady=10)

        self.temp_var    = tk.DoubleVar(value=0.7)
        self.topp_var    = tk.DoubleVar(value=0.95)
        self.topk_var    = tk.IntVar(value=0)
        self.maxtok_var  = tk.IntVar(value=800)
        self.freqp_var   = tk.DoubleVar(value=0.03)
        self.presp_var   = tk.DoubleVar(value=0.03)

        row = 0
        self._make_slider(inner, "Temperature", self.temp_var, 0.0, 2.0, row,
                          "Higher = more creative/random. Lower = more focused/deterministic.", resolution=0.05)
        row += 1
        self._make_slider(inner, "Top P", self.topp_var, 0.0, 1.0, row,
                          "Nucleus sampling. 0.95 means top 95% probability tokens.", resolution=0.05)
        row += 1
        self._make_slider(inner, "Top K", self.topk_var, 0, 100, row,
                          "Limits to top K tokens. 0 = disabled.", resolution=1)
        row += 1
        self._make_slider(inner, "Max Tokens", self.maxtok_var, 100, 4000, row,
                          "Maximum response length in tokens.", resolution=50)
        row += 1
        self._make_slider(inner, "Frequency Penalty", self.freqp_var, 0.0, 2.0, row,
                          "Penalizes repeated tokens. Higher = less repetition.", resolution=0.05)
        row += 1
        self._make_slider(inner, "Presence Penalty", self.presp_var, 0.0, 2.0, row,
                          "Penalizes tokens already present. Higher = more diverse topics.", resolution=0.05)

        inner.columnconfigure(1, weight=1)

    def _build_prompt_section(self, container, pad, title, config_key, tooltip, height=8):
        frame = tk.LabelFrame(container, text=f"  📝 {title}  ", bg=BG_CARD, fg=ACCENT,
                              font=("Segoe UI", 11, "bold"), bd=1, relief="solid",
                              highlightbackground=BORDER, highlightthickness=1)
        frame.pack(fill="x", padx=15, pady=(3, 3))

        inner = tk.Frame(frame, bg=BG_CARD)
        inner.pack(fill="x", padx=15, pady=10)

        desc = tk.Label(inner, text=tooltip, bg=BG_CARD, fg=TEXT_DIM,
                        font=("Segoe UI", 9), anchor="w")
        desc.pack(fill="x", pady=(0, 5))

        text_widget = tk.Text(inner, height=height, bg=BG_INPUT, fg=TEXT,
                              insertbackground=TEXT, font=("Consolas", 10),
                              relief="flat", bd=0, wrap="word", padx=8, pady=6,
                              selectbackground=ACCENT, selectforeground="white")
        text_widget.pack(fill="x")

        setattr(self, f"{config_key}_text", text_widget)

    def _make_label(self, parent, text, row, tooltip=""):
        label = tk.Label(parent, text=text, bg=BG_CARD, fg=TEXT_LABEL,
                         font=("Segoe UI", 10), anchor="w")
        label.grid(row=row, column=0, sticky="w", padx=(0, 15), pady=3)
        if tooltip:
            ToolTip(label, tooltip)

    def _make_entry(self, parent, var, row):
        entry = tk.Entry(parent, textvariable=var, bg=BG_INPUT, fg=TEXT,
                         insertbackground=TEXT, font=("Consolas", 10),
                         relief="flat", bd=0)
        entry.grid(row=row, column=1, sticky="ew", pady=3, ipady=6)

    def _make_slider(self, parent, label_text, var, from_, to_, row, tooltip="", resolution=0.01):
        label = tk.Label(parent, text=label_text, bg=BG_CARD, fg=TEXT_LABEL,
                         font=("Segoe UI", 10), anchor="w", width=18)
        label.grid(row=row, column=0, sticky="w", padx=(0, 10), pady=4)
        if tooltip:
            ToolTip(label, tooltip)

        slider_frame = tk.Frame(parent, bg=BG_CARD)
        slider_frame.grid(row=row, column=1, sticky="ew", pady=4)
        slider_frame.columnconfigure(0, weight=1)

        val_label = tk.Label(slider_frame, text=str(var.get()), bg=BG_CARD, fg=ACCENT,
                             font=("Segoe UI Semibold", 10), width=6, anchor="e")
        val_label.grid(row=0, column=1, padx=(8, 0))

        slider = tk.Scale(slider_frame, variable=var, from_=from_, to=to_,
                          orient="horizontal", resolution=resolution,
                          bg=BG_CARD, fg=TEXT, troughcolor=BG_INPUT,
                          highlightthickness=0, sliderrelief="flat",
                          activebackground=ACCENT, font=("Segoe UI", 1),
                          showvalue=False, length=350,
                          command=lambda v, vl=val_label: vl.configure(text=str(v)))
        slider.grid(row=0, column=0, sticky="ew")

    def _toggle_key(self):
        if self.show_key.get():
            self.key_entry.configure(show="•")
            self.show_key.set(False)
        else:
            self.key_entry.configure(show="")
            self.show_key.set(True)

    def _toggle_tts_key(self):
        if self.show_tts_key.get():
            self.tts_key_entry.configure(show="•")
            self.show_tts_key.set(False)
        else:
            self.tts_key_entry.configure(show="")
            self.show_tts_key.set(True)

    def _load_config(self):
        try:
            if os.path.exists(CONFIG_PATH):
                with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                    data = json.load(f)
                self.config = {**DEFAULTS, **data}
                self._set_status("✅ Configuration loaded from file.", SUCCESS)
            else:
                self.config = dict(DEFAULTS)
                self._set_status("ℹ️ No config file found. Using defaults.", WARNING)

            # Apply to UI
            self.base_url_var.set(self.config["base_url"])
            self.api_key_var.set(self.config["api_key"])
            self.model_var.set(self.config["model"])
            self.temp_var.set(self.config["temperature"])
            self.topp_var.set(self.config["top_p"])
            self.topk_var.set(self.config["top_k"])
            self.maxtok_var.set(self.config["max_tokens"])
            self.freqp_var.set(self.config["frequency_penalty"])
            self.presp_var.set(self.config["presence_penalty"])

            self.tts_enable_var.set(self.config.get("tts_enable", DEFAULTS["tts_enable"]))
            self.tts_provider_var.set(self.config.get("tts_provider", DEFAULTS["tts_provider"]))
            self.tts_base_url_var.set(self.config.get("tts_base_url", DEFAULTS["tts_base_url"]))
            self.tts_api_key_var.set(self.config.get("tts_api_key", DEFAULTS["tts_api_key"]))
            self.tts_model_var.set(self.config.get("tts_model", DEFAULTS["tts_model"]))
            self.tts_region_var.set(self.config.get("tts_region", DEFAULTS["tts_region"]))

            self.system_prompt_text.delete("1.0", "end")
            self.system_prompt_text.insert("1.0", self.config["system_prompt"])
            self.post_history_prompt_text.delete("1.0", "end")
            self.post_history_prompt_text.insert("1.0", self.config["post_history_prompt"])

        except Exception as e:
            self._set_status(f"❌ Error loading config: {e}", ERROR)

    def _save_config(self):
        try:
            data = {
                "base_url":             self.base_url_var.get().strip(),
                "api_key":              self.api_key_var.get().strip(),
                "model":                self.model_var.get().strip(),
                "system_prompt":        self.system_prompt_text.get("1.0", "end-1c").strip(),
                "post_history_prompt":  self.post_history_prompt_text.get("1.0", "end-1c").strip(),
                "temperature":          round(self.temp_var.get(), 2),
                "top_p":                round(self.topp_var.get(), 2),
                "top_k":                self.topk_var.get(),
                "max_tokens":           self.maxtok_var.get(),
                "frequency_penalty":    round(self.freqp_var.get(), 2),
                "presence_penalty":     round(self.presp_var.get(), 2),
                "tts_enable":           self.tts_enable_var.get(),
                "tts_provider":         self.tts_provider_var.get(),
                "tts_base_url":         self.tts_base_url_var.get().strip(),
                "tts_api_key":          self.tts_api_key_var.get().strip(),
                "tts_model":            self.tts_model_var.get().strip(),
                "tts_region":           self.tts_region_var.get().strip(),
            }

            if not data["api_key"]:
                self._set_status("⚠️ Warning: API Key is empty!", WARNING)

            os.makedirs(os.path.dirname(CONFIG_PATH), exist_ok=True)
            with open(CONFIG_PATH, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2, ensure_ascii=False)

            self._set_status(f"✅ Configuration saved! ({CONFIG_PATH})", SUCCESS)

        except Exception as e:
            self._set_status(f"❌ Error saving: {e}", ERROR)

    def _reset_defaults(self):
        if messagebox.askyesno("Reset to Defaults", "Reset all settings to defaults?\nThis won't save until you click Save."):
            self.config = dict(DEFAULTS)
            self.base_url_var.set(DEFAULTS["base_url"])
            self.api_key_var.set(DEFAULTS["api_key"])
            self.model_var.set(DEFAULTS["model"])
            self.temp_var.set(DEFAULTS["temperature"])
            self.topp_var.set(DEFAULTS["top_p"])
            self.topk_var.set(DEFAULTS["top_k"])
            self.maxtok_var.set(DEFAULTS["max_tokens"])
            self.freqp_var.set(DEFAULTS["frequency_penalty"])
            self.presp_var.set(DEFAULTS["presence_penalty"])
            self.tts_enable_var.set(DEFAULTS["tts_enable"])
            self.tts_provider_var.set(DEFAULTS["tts_provider"])
            self.tts_base_url_var.set(DEFAULTS["tts_base_url"])
            self.tts_api_key_var.set(DEFAULTS["tts_api_key"])
            self.tts_model_var.set(DEFAULTS["tts_model"])
            self.tts_region_var.set(DEFAULTS["tts_region"])
            self.system_prompt_text.delete("1.0", "end")
            self.system_prompt_text.insert("1.0", DEFAULTS["system_prompt"])
            self.post_history_prompt_text.delete("1.0", "end")
            self.post_history_prompt_text.insert("1.0", DEFAULTS["post_history_prompt"])
            self._set_status("🔄 Reset to defaults. Click Save to apply.", WARNING)

    def _set_status(self, msg, color=TEXT):
        self.status_var.set(msg)
        self.status_label.configure(fg=color)
        # Auto-clear after 5 seconds
        self.root.after(5000, lambda: self.status_var.set(""))


def main():
    root = tk.Tk()
    app = AI2UConfigurator(root)
    root.mainloop()


if __name__ == "__main__":
    main()
