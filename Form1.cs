using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KoreanToJapaneseTTS
{
    public partial class Form1 : Form
    {
        private SpeechSynthesizer? synthesizer;
        private readonly HttpClient httpClient;
        private string azureApiKey = string.Empty;
        private string azureRegion = string.Empty;
        private bool isMonitoring;
        private List<(char Key, bool IsHangulMode, bool IsShift, bool IsHangulKey, int Position)> inputBuffer;
        private StringBuilder displayBuffer;
        private IntPtr keyboardHookId = IntPtr.Zero;
        private IntPtr shellHookId = IntPtr.Zero;
        private IntPtr lastActiveWindowHandle = IntPtr.Zero;
        private uint lastActiveProcessId = 0;
        private const string IniFilePath = "config.ini";
        private bool isHangulMode;
        private bool isShiftPressed;
        private System.Timers.Timer inputTimeoutTimer;
        private bool isFirstEnter;
        private string lastSegmentLanguage;
        private int hangulKeyPressCountAfterLastKey;
        private int inputPositionCounter;
        private List<(int Position, bool IsHangulMode)> hangulKeyToggles;
        private bool ignoreHangulKeyUntilFirstSegment; // 추가: 첫 세그먼트 전 한/영 키 입력 무시 플래그
        private bool isFirstFocusWindow = true; // 첫 포커스 창 여부
        private string currentInputLanguage = "English"; // 한/영 키 기준으로 현재 입력 언어 상태 추적
        private int lastSegmentEndPosition = 0; // 마지막 세그먼트의 마지막 입력 위치
        private List<List<(char, bool, bool, bool, int)>> segmentedBuffer = new();


        private static readonly Dictionary<char, string> EnglishToKoreanMap = new Dictionary<char, string>
        {
            {'a', "ㅁ"}, {'b', "ㅠ"}, {'c', "ㅊ"}, {'d', "ㅇ"}, {'e', "ㄷ"},
            {'f', "ㄹ"}, {'g', "ㅎ"}, {'h', "ㅗ"}, {'i', "ㅑ"}, {'j', "ㅓ"},
            {'k', "ㅏ"}, {'l', "ㅣ"}, {'m', "ㅡ"}, {'n', "ㅜ"}, {'o', "ㅐ"},
            {'p', "ㅔ"}, {'q', "ㅂ"}, {'r', "ㄱ"}, {'s', "ㄴ"}, {'t', "ㅅ"},
            {'u', "ㅕ"}, {'v', "ㅍ"}, {'w', "ㅈ"}, {'x', "ㅌ"}, {'y', "ㅛ"},
            {'z', "ㅋ"},
            {'0', "0"}, {'1', "1"}, {'2', "2"}, {'3', "3"}, {'4', "4"},
            {'5', "5"}, {'6', "6"}, {'7', "7"}, {'8', "8"}, {'9', "9"},
            {'.', "."}, {',', ","}, {'!', "!"}, {'?', "?"}, {'@', "@"},
            {'#', "#"}, {'$', "$"}, {'%', "%"}, {'&', "&"}, {'*', "*"},
            {'(', "("}, {')', ")"}, {'-', "-"}, {'+', "+"}, {'=', "="},
            {'[', "["}, {']', "]"}, {'{', "{"}, {'}', "}"}, {'<', "<"}, {'>', ">"}
        };

        private static readonly Dictionary<string, string> EnhancedJamoMap = new Dictionary<string, string>
        {
            {"ㄱ", "ㄲ"}, {"ㄷ", "ㄸ"}, {"ㅂ", "ㅃ"}, {"ㅅ", "ㅆ"}, {"ㅈ", "ㅉ"}
        };

        private static readonly HashSet<char> AllowedSpecialChars = new HashSet<char>
        {
            '.', ',', '!', '?', '@', '#', '$', '%', '&', '*', '(', ')', '-', '+', '=', '[', ']', '{', '}', '<', '>'
        };

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle([In] string? lpModuleName);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetKeyboardState(byte[] lpKeyState);
        [DllImport("user32.dll")]
        private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterShellHookWindow(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DeregisterShellHookWindow(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_HANGUL = 0x15;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int HSHELL_WINDOWACTIVATED = 4;

        public Form1()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            displayBuffer = new StringBuilder();
            inputBuffer = new List<(char, bool, bool, bool, int)>();
            hangulKeyToggles = new List<(int, bool)>();
            isMonitoring = false;
            isHangulMode = false;
            isShiftPressed = false;
            isFirstEnter = true;
            lastSegmentLanguage = "English";
            hangulKeyPressCountAfterLastKey = 0;
            inputPositionCounter = 0;
            isFirstFocusWindow = true; // 첫 포커스 창 플래그 초기화
            ignoreHangulKeyUntilFirstSegment = true; // 첫 포커스 창에서 한/영 키 무시

            inputTimeoutTimer = new System.Timers.Timer(15000);
            inputTimeoutTimer.Elapsed += (s, e) =>
            {
                ResetInputBuffers();
                Debug.WriteLine("타이머 만료: 입력 버퍼 초기화 (isFirstEnter 유지)");
            };
            inputTimeoutTimer.AutoReset = false;

            LoadCredentialsFromIni();
            InitializeTTS();
            SetupKeyboardHook();
            SetupShellHook();
            speechRateTrackBar.Value = 0;
            UpdateSpeechRate();
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            statusLabel.Text = string.Empty;
            InitializeActiveWindow(); // 추가: 초기 포커스 창 처리
        }

        private void InitializeActiveWindow()
        {
            try
            {
                IntPtr activeWindow = GetForegroundWindow();
                if (activeWindow == IntPtr.Zero)
                {
                    Debug.WriteLine("초기 활성 창 핸들 가져오기 실패");
                    return;
                }

                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(activeWindow, windowTitle, 256);
                uint processId = 0;
                GetWindowThreadProcessId(activeWindow, out processId);

                lastActiveWindowHandle = activeWindow;
                lastActiveProcessId = processId;
                ResetInputBuffers();
                isFirstEnter = true;
                isHangulMode = false;
                isShiftPressed = false;
                lastSegmentLanguage = "English";
                hangulKeyPressCountAfterLastKey = 0;
                inputPositionCounter = 0;
                hangulKeyToggles.Clear();
                ignoreHangulKeyUntilFirstSegment = isFirstFocusWindow; // 첫 포커스 창에서만 한/영 키 무시
                UpdateStatusLabel($"초기 활성 창 설정: {windowTitle} (PID: {processId})");
                Debug.WriteLine($"초기 활성 창 설정: Handle={activeWindow}, Title={windowTitle}, ProcessId={processId}, isFirstFocusWindow={isFirstFocusWindow}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"초기 활성 창 설정 오류: {ex.Message}");
                UpdateStatusLabel($"초기 활성 창 설정 오류: {ex.Message}");
            }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            CleanupResources();
        }

        private void SetupKeyboardHook()
        {
            try
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule? curModule = curProcess.MainModule)
                {
                    if (curModule?.ModuleName == null) throw new Exception("모듈 이름을 찾을 수 없습니다.");
                    keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardProc, GetModuleHandle(curModule.ModuleName), 0);
                    if (keyboardHookId == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "키보드 훅 설정 실패");
                    Debug.WriteLine($"키보드 훅 설정 완료: {keyboardHookId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"키보드 훅 설정 오류: {ex.Message}");
                if (IsHandleCreated)
                    statusLabel.Text = $"키보드 훅 설정 오류: {ex.Message}";
            }
        }

        private void SetupShellHook()
        {
            try
            {
                if (!RegisterShellHookWindow(Handle))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "쉘 훅 설정 실패");
                shellHookId = Handle;
                Debug.WriteLine("쉘 훅 설정 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"쉘 훅 설정 오류: {ex.Message}");
                if (IsHandleCreated)
                    statusLabel.Text = $"쉘 훅 설정 오류: {ex.Message}";
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int HSHELL_WINDOWACTIVATED = 4;
            const int WM_ACTIVATEAPP = 0x001C;

            if (m.Msg == WM_ACTIVATEAPP)
            {
                if (m.WParam.ToInt32() == 0) // 앱이 비활성화될 때
                {
                    Debug.WriteLine("WM_ACTIVATEAPP → 애플리케이션 포커스 아웃 감지");
                    ResetInputStateDueToFocusLoss();
                }
            }
            else if (m.Msg == 0x031D && (int)m.WParam == HSHELL_WINDOWACTIVATED)
            {
                if (m.LParam == IntPtr.Zero)
                {
                    Debug.WriteLine("창 핸들 유효하지 않음, 포커스 이벤트 무시");
                    base.WndProc(ref m);
                    return;
                }

                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(m.LParam, windowTitle, 256);
                uint processId = 0;
                GetWindowThreadProcessId(m.LParam, out processId);

                Debug.WriteLine($"창 포커스 변경 감지: Handle={m.LParam}, Title={windowTitle}, ProcessId={processId}");

                // ✅ 이전 창이 포커스를 잃었을 때
                if (lastActiveWindowHandle != IntPtr.Zero && m.LParam != lastActiveWindowHandle)
                {
                    Debug.WriteLine("이전 창 포커스 손실 → 입력 상태 초기화");
                    ResetInputStateDueToFocusLoss();
                }

                // ✅ 현재 포커스된 창 상태 저장 및 리셋
                if (m.LParam != lastActiveWindowHandle || processId != lastActiveProcessId)
                {
                    lastActiveWindowHandle = m.LParam;
                    lastActiveProcessId = processId;
                    ResetInputBuffers();
                    isFirstEnter = true;
                    isHangulMode = false;
                    isShiftPressed = false;
                    lastSegmentLanguage = "English";
                    hangulKeyPressCountAfterLastKey = 0;
                    inputPositionCounter = 0;
                    hangulKeyToggles.Clear();
                    ignoreHangulKeyUntilFirstSegment = isFirstFocusWindow;
                    isFirstFocusWindow = false;

                    UpdateStatusLabel($"창 포커스 변경, 입력 대기: {windowTitle} (PID: {processId})");
                }
            }

            base.WndProc(ref m);
        }
        private void ResetInputStateDueToFocusLoss()
        {
            isFirstEnter = true;
            ignoreHangulKeyUntilFirstSegment = true;
            hangulKeyToggles.Clear();
            isHangulMode = false;
            isShiftPressed = false;
            Debug.WriteLine("입력 상태 리셋: isFirstEnter, ignoreHangulKeyUntilFirstSegment, hangulKeyToggles, isHangulMode");
        }

        private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;
                bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN;
                bool isKeyUp = wParam == (IntPtr)WM_KEYUP;

                Debug.WriteLine($"키 이벤트: vkCode={vkCode}, key={key}, 상태={(isKeyDown ? "Down" : "Up")}, 입력기: {(isHangulMode ? "한국어" : "영어")}, isFirstEnter={isFirstEnter}, lastSegmentLanguage={lastSegmentLanguage}, ignoreHangulKeyUntilFirstSegment={ignoreHangulKeyUntilFirstSegment}");

                if (key == Keys.LShiftKey || key == Keys.RShiftKey)
                {
                    isShiftPressed = isKeyDown;
                    Debug.WriteLine($"Shift 키 상태 업데이트: isShiftPressed={isShiftPressed}");
                    return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
                }

                if (isMonitoring && isKeyDown && !IsControlKey(key))
                {
                    inputTimeoutTimer.Stop();
                    inputTimeoutTimer.Start();

                    if (key == Keys.HangulMode)
                    {
                        if (!ignoreHangulKeyUntilFirstSegment)
                        {
                            if (inputBuffer.Count > 0 && inputBuffer.Any(t => !t.IsHangulKey))
                            {
                                segmentedBuffer.Add(new List<(char, bool, bool, bool, int)>(inputBuffer));
                                ResetInputBuffers(keepState: true);
                            }

                            lastSegmentLanguage = lastSegmentLanguage == "Korean" ? "English" : "Korean";
                            Debug.WriteLine($"한/영 키 언어 전환 → 새 언어: {lastSegmentLanguage}");
                        }

                        isHangulMode = !isHangulMode;
                        currentInputLanguage = isHangulMode ? "Korean" : "English";

                        hangulKeyPressCountAfterLastKey++;
                        inputBuffer.Add(('\0', isHangulMode, false, true, inputPositionCounter));
                        hangulKeyToggles.Add((inputPositionCounter, isHangulMode));
                        inputPositionCounter++;

                        UpdateStatusLabel($"입력기 전환: {(isHangulMode ? "한글" : "영어")}");
                        Debug.WriteLine($"한/영 키 입력 → 현재 입력기 상태: {currentInputLanguage}");
                        return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
                    }
                    else if (key == Keys.Enter || key == Keys.Tab)
                    {
                        string inputText = string.Join("", inputBuffer.Where(t => !t.IsHangulKey).Select(t => t.Key)).Trim();
                        Debug.WriteLine($"{(key == Keys.Enter ? "Enter" : "Tab")} 입력: {inputText}, isFirstEnter={isFirstEnter}");
                        if (!string.IsNullOrEmpty(inputText))
                        {
                            ProcessInputBuffer();
                        }
                        hangulKeyPressCountAfterLastKey = 0;
                        inputPositionCounter = 0;
                        if (key == Keys.Enter)
                        {
                            isFirstEnter = false;

                            // ✅ 바로 아래 줄을 추가하세요
                            ignoreHangulKeyUntilFirstSegment = false;
                        }
                        ResetInputBuffers();
                    }
                    else if (key == Keys.Back)
                    {
                        if (inputBuffer.Count > 0 && !inputBuffer[inputBuffer.Count - 1].IsHangulKey)
                        {
                            inputBuffer.RemoveAt(inputBuffer.Count - 1);
                            displayBuffer.Length--;
                            inputPositionCounter--;
                            UpdateGui();
                        }
                    }
                    else if (key == Keys.Space)
                    {
                        inputBuffer.Add((' ', isHangulMode, isShiftPressed, false, inputPositionCounter++));
                        displayBuffer.Append(" ");
                        UpdateGui();
                        hangulKeyPressCountAfterLastKey = 0;
                    }
                    else
                    {
                        (string currentKey, bool isKoreanIME) = GetKeyboardChar((uint)vkCode);
                        if (!string.IsNullOrEmpty(currentKey) && IsValidInputChar(currentKey))
                        {
                            char c = currentKey[0];
                            inputBuffer.Add((c, isKoreanIME, isShiftPressed, false, inputPositionCounter++));
                            displayBuffer.Append(c);
                            UpdateGui();
                            UpdateStatusLabel($"문자: {c}, IME: {(isKoreanIME ? "한국어" : "영어")}, Shift: {isShiftPressed}");
                            Debug.WriteLine($"입력 문자: {c}, 한국어 IME: {isKoreanIME}, Shift: {isShiftPressed}, 위치: {inputPositionCounter - 1}, 입력기 상태: {(isHangulMode ? "한국어" : "영어")}");
                            hangulKeyPressCountAfterLastKey = 0;
                        }
                    }
                }
            }
            return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
        }

        private void UpdateGui()
        {
            if (IsHandleCreated)
            {
                Invoke((MethodInvoker)(() =>
                {
                    inputTextBox.Text = displayBuffer.ToString();
                    inputTextBox.SelectionStart = inputTextBox.Text.Length;
                    inputTextBox.ScrollToCaret();
                }));
            }
        }
        private void ResetInputBuffers(bool keepState = false)
        {
            inputBuffer.Clear();
            displayBuffer.Clear();
            hangulKeyToggles.Clear();
            hangulKeyPressCountAfterLastKey = 0;
            inputPositionCounter = 0;

            if (!keepState)
            {
                isShiftPressed = false;
                isHangulMode = false;
            }

            UpdateGui();
            Debug.WriteLine("입력 버퍼 초기화" + (keepState ? " (상태 유지)" : ""));
        }

        private async void ProcessInputBuffer()
        {
            try
            {
                Debug.WriteLine($"ProcessInputBuffer 시작: isHangulMode={isHangulMode}, isFirstEnter={isFirstEnter}, 입력: {string.Join("", inputBuffer.Where(t => !t.IsHangulKey).Select(t => t.Key))}");

                ignoreHangulKeyUntilFirstSegment = false;

                var segments = new List<(string Text, List<(char, bool, bool, bool, int)> Buffer, string Language, List<int> ShiftPositions)>();

                // ✅ 한/영 키로 분리된 세그먼트 먼저 처리
                foreach (var seg in segmentedBuffer)
                {
                    if (seg.Count == 0) continue;

                    var segmentText = string.Join("", seg.Where(t => !t.Item4).Select(t => t.Item1));
                    var segmentMode = seg.FirstOrDefault().Item2;
                    var segmentLang = DetermineSegmentLanguage(segments, seg, segmentMode);
                    var shiftPos = seg.Where(t => t.Item3).Select(t => t.Item5).ToList();

                    segments.Add((segmentText, new List<(char, bool, bool, bool, int)>(seg), segmentLang, shiftPos));
                }
                segmentedBuffer.Clear();

                var currentSegment = new List<(char, bool, bool, bool, int)>();
                var currentShiftPositions = new List<int>();
                string currentText = string.Empty;
                bool? currentMode = null;

                foreach (var (key, mode, isShift, isHangulKey, position) in inputBuffer)
                {
                    if (isHangulKey)
                    {
                        if (currentText.Length > 0)
                        {
                            string lang = DetermineSegmentLanguage(segments, currentSegment, currentMode ?? mode);
                            segments.Add((currentText, currentSegment.ToList(), lang, currentShiftPositions.ToList()));
                            currentSegment.Clear();
                            currentShiftPositions.Clear();
                            currentText = string.Empty;
                            currentMode = mode;
                        }
                        continue;
                    }

                    if (key == ' ')
                    {
                        if (currentText.Length > 0)
                        {
                            string lang = DetermineSegmentLanguage(segments, currentSegment, currentMode ?? mode);
                            segments.Add((currentText, currentSegment.ToList(), lang, currentShiftPositions.ToList()));
                            currentSegment.Clear();
                            currentShiftPositions.Clear();
                            currentText = string.Empty;
                            currentMode = null;
                        }
                        continue;
                    }

                    if (currentMode == null && !char.IsDigit(key) && !AllowedSpecialChars.Contains(key))
                    {
                        currentMode = mode;
                    }

                    if (isShift)
                        currentShiftPositions.Add(position);

                    currentSegment.Add((key, mode, isShift, isHangulKey, position));
                    currentText += key;
                }

                if (currentText.Length > 0)
                {
                    string lang = DetermineSegmentLanguage(segments, currentSegment, currentMode ?? isHangulMode);
                    segments.Add((currentText, currentSegment.ToList(), lang, currentShiftPositions.ToList()));
                }

                StringBuilder finalTtsText = new StringBuilder();
                StringBuilder displayText = new StringBuilder();

                foreach (var (text, buffer, lang, shiftPositions) in segments)
                {
                    Debug.WriteLine($"세그먼트 처리: 입력={text}, 언어={lang}, Shift 위치={(shiftPositions.Any() ? string.Join(", ", shiftPositions) : "없음")}");

                    if (lang == "Special")
                    {
                        finalTtsText.Append(text).Append(" ");
                        displayText.Append(text).Append(" ");
                    }
                    else if (lang == "Korean")
                    {
                        bool hasKoreanChars = text.Any(c => c >= 0xAC00 && c <= 0xD7A3);
                        string processedText;

                        if (hasKoreanChars)
                        {
                            processedText = text;
                            Debug.WriteLine($"한글 문자 감지: {text}");
                        }
                        else
                        {
                            processedText = ConvertEnglishToKorean(text, buffer);
                            if (string.IsNullOrEmpty(processedText))
                            {
                                Debug.WriteLine($"한글 조합 실패: {text}");
                                if (lang == "Korean")
                                {
                                    processedText = text;
                                    Debug.WriteLine($"한글 상태이므로 원문 그대로 번역 시도: {processedText}");
                                }
                                else
                                {
                                    Debug.WriteLine($"비한글 상태에서 조합 실패 → 처리 생략");
                                    continue;
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"한글 조합 성공: {text} → {processedText}");
                            }
                        }

                        string japaneseText = await TranslateToJapanese(processedText);
                        Debug.WriteLine($"일본어 번역: {processedText} → {japaneseText}");
                        finalTtsText.Append(japaneseText).Append(" ");
                        displayText.Append(processedText).Append(" ");
                    }
                    else if (lang == "English")
                    {
                        finalTtsText.Append(text).Append(" ");
                        displayText.Append(text).Append(" ");
                        Debug.WriteLine($"영어 세그먼트: 번역 없이 일본어 TTS로 직접 재생 → {text}");
                    }
                }

                string ttsText = finalTtsText.ToString().Trim();
                string finalDisplayText = displayText.ToString().Trim();

                displayBuffer.Clear();
                displayBuffer.Append(finalDisplayText);
                UpdateGui();

                if (!string.IsNullOrEmpty(ttsText) && synthesizer != null)
                {
                    synthesizer.SpeakAsync(ttsText);
                    UpdateStatusLabel($"재생 중: '{ttsText}', 언어: {lastSegmentLanguage}");
                }
                else
                {
                    Debug.WriteLine("TTS 텍스트 없음 또는 synthesizer null");
                    UpdateStatusLabel("재생 실패: 텍스트 없음");
                }

                if (segments.Any())
                {
                    lastSegmentLanguage = segments.Last().Language;
                    lastSegmentEndPosition = segments.Last().Buffer.Last().Item5;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"입력 버퍼 처리 오류: {ex.Message}");
                UpdateStatusLabel($"오류: {ex.Message}");
            }
        }

        private string DetermineSegmentLanguage(
            List<(string Text, List<(char, bool, bool, bool, int)> Buffer, string Language, List<int> ShiftPositions)> allSegments,
            List<(char Key, bool IsHangulMode, bool IsShift, bool IsHangulKey, int Position)> segment,
            bool mode)
        {
            if (segment.All(t => char.IsDigit(t.Key) || AllowedSpecialChars.Contains(t.Key)))
            {
                Debug.WriteLine("세그먼트: 특수/숫자");
                return "Special";
            }

            // ✅ 입력값 기준 세그먼트 그룹핑 후 다수결 언어 판단
            string currentText = string.Join("", segment.Where(t => !t.IsHangulKey).Select(t => t.Key));

            // 동일한 텍스트 그룹 수집
            var grouped = allSegments
                .GroupBy(s => s.Text)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (grouped.TryGetValue(currentText, out var matchingSegments))
            {
                int koreanCount = matchingSegments.Count(s => s.Language == "Korean");
                int englishCount = matchingSegments.Count(s => s.Language == "English");

                if (koreanCount > englishCount)
                {
                    Debug.WriteLine($"다수결: '{currentText}' → Korean (K:{koreanCount} / E:{englishCount})");
                    return "Korean";
                }
                else if (englishCount > koreanCount)
                {
                    Debug.WriteLine($"다수결: '{currentText}' → English (K:{koreanCount} / E:{englishCount})");
                    return "English";
                }
            }

            // 기본 로직 유지
            bool hasKoreanChars = segment.Any(t => !t.IsHangulKey && (
                (t.Key >= 0xAC00 && t.Key <= 0xD7A3) ||
                "ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣㄳㄵㄶㄺㄻㄼㄽㄾㄿㅀㅄ".Contains(t.Key)));

            int segmentStartPosition = segment.Any() ? segment.First().Position : inputPositionCounter;
            int hangulKeyCountBeforeSegment = hangulKeyToggles.Count(t => t.Position < segmentStartPosition);
            bool isAfterFirstEnter = !isFirstEnter;
            if (hasKoreanChars && !isAfterFirstEnter)
            {
                return "Korean";
            }

            if (isFirstEnter)
            {
                int koreanSegments = allSegments.Count(s => s.Language == "Korean") + (mode ? 1 : 0);
                int totalSegments = allSegments.Count + 1;
                bool isMajorityKorean = koreanSegments > (totalSegments / 2);
                string defaultLanguage = isMajorityKorean ? "Korean" : "English";

                bool isOddHangulKeyPress = hangulKeyCountBeforeSegment % 2 == 1;
                return isOddHangulKeyPress ? (defaultLanguage == "Korean" ? "English" : "Korean") : defaultLanguage;
            }

            int hangulKeyCountSinceLastSegment = hangulKeyToggles.Count(t => t.Position > lastSegmentEndPosition);
            bool isOddToggleCount = hangulKeyCountSinceLastSegment % 2 == 1;
            string newLang = isOddToggleCount
                ? (lastSegmentLanguage == "Korean" ? "English" : "Korean")
                : lastSegmentLanguage;

            Debug.WriteLine($"첫 엔터 이후 언어 판별: lastLang={lastSegmentLanguage}, 토글 수={hangulKeyCountSinceLastSegment}, 결과={newLang}");
            return newLang;
        }



        private void ResetInputBuffers()
        {
            inputBuffer.Clear();
            displayBuffer.Clear();
            hangulKeyToggles.Clear();
            hangulKeyPressCountAfterLastKey = 0;
            inputPositionCounter = 0;
            // 수정: ignoreHangulKeyUntilFirstSegment는 창 포커스 변경 시에만 초기화되므로 여기서 유지
            UpdateGui();
            Debug.WriteLine("입력 버퍼 초기화");
        }

        private bool IsValidInputChar(string input)
        {
            if (input.Length != 1) return false;
            char c = input[0];
            return char.IsLetter(c) || char.IsDigit(c) || AllowedSpecialChars.Contains(c) ||
                   (c >= 0xAC00 && c <= 0xD7A3) ||
                   "ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣㄳㄵㄶㄺㄻㄼㄽㄾㄿㅀㅄ".Contains(c);
        }

        private bool IsControlKey(Keys key)
        {
            return key == Keys.LShiftKey || key == Keys.RShiftKey || key == Keys.Control || key == Keys.Alt ||
                   key == Keys.LWin || key == Keys.RWin;
        }

        private (string, bool) GetKeyboardChar(uint vkCode)
        {
            try
            {
                if (vkCode == (uint)Keys.LShiftKey || vkCode == (uint)Keys.RShiftKey || vkCode == VK_CONTROL || vkCode == VK_MENU || vkCode == VK_LWIN || vkCode == VK_RWIN)
                    return (string.Empty, isHangulMode);

                StringBuilder sb = new StringBuilder(32);
                byte[] vkBuffer = new byte[256];
                if (!GetKeyboardState(vkBuffer))
                {
                    Debug.WriteLine($"GetKeyboardState 실패, 오류 코드: {Marshal.GetLastWin32Error()}");
                    return (string.Empty, isHangulMode);
                }
                uint scanCode = MapVirtualKey(vkCode, 0);
                IntPtr keyboardLayout = GetKeyboardLayout(0);
                int result = ToUnicodeEx(vkCode, scanCode, vkBuffer, sb, sb.Capacity, 0, keyboardLayout);

                uint keyboardLayoutId = (uint)keyboardLayout & 0xFFFF;
                bool isKoreanIME = keyboardLayoutId == 0x0412;
                string inputChar = sb.ToString();

                bool isHangulChar = inputChar.Length == 1 && (
                    (inputChar[0] >= 0xAC00 && inputChar[0] <= 0xD7A3) ||
                    "ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣㄳㄵㄶㄺㄻㄼㄽㄾㄿㅀㅄ".Contains(inputChar));

                Debug.WriteLine($"키 문자 변환: 입력='{inputChar}', 키코드={vkCode}, isKoreanIME={isKoreanIME}, isHangulChar={isHangulChar}, LayoutId=0x{keyboardLayoutId:X}");

                if (result <= 0)
                {
                    Debug.WriteLine($"ToUnicodeEx 실패, 결과: {result}, 키코드: {vkCode}");
                    return (string.Empty, isHangulMode);
                }

                return (inputChar, isKoreanIME || isHangulChar);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"키보드 문자 가져오기 오류: {ex.Message}, 키코드: {vkCode}");
                return (string.Empty, isHangulMode);
            }
        }

        private string ConvertEnglishToKorean(string inputText, List<(char Key, bool IsHangulMode, bool IsShift, bool IsHangulKey, int Position)> inputBufferWithMode)
        {
            List<(string Jamo, bool IsShift, int Position)> jamoList = new();
            int bufferIndex = 0;

            for (int i = 0; i < inputText.Length; i++)
            {
                char c = inputText[i];
                if (c == ' ')
                {
                    jamoList.Add((" ", false, inputBufferWithMode[bufferIndex].Position));
                    bufferIndex++;
                    continue;
                }

                if (bufferIndex >= inputBufferWithMode.Count || inputBufferWithMode[bufferIndex].Key != c)
                {
                    Debug.WriteLine($"버퍼 불일치: 입력={c}, 버퍼 인덱스={bufferIndex}");
                    return string.Empty;
                }

                bool isShiftPressed = inputBufferWithMode[bufferIndex].IsShift;
                int position = inputBufferWithMode[bufferIndex].Position;

                if (c >= 0xAC00 && c <= 0xD7A3)
                {
                    jamoList.Add((c.ToString(), isShiftPressed, position));
                    bufferIndex++;
                    continue;
                }

                if (EnglishToKoreanMap.TryGetValue(c, out string? jamo))
                {
                    if (i + 1 < inputText.Length && bufferIndex + 1 < inputBufferWithMode.Count)
                    {
                        char nextChar = inputText[i + 1];
                        if (EnglishToKoreanMap.TryGetValue(nextChar, out string? nextJamo))
                        {
                            string? combinedVowel = CombineVowels(jamo, nextJamo);
                            if (combinedVowel != null)
                            {
                                jamoList.Add((combinedVowel, isShiftPressed, position));
                                i++;
                                bufferIndex += 2;
                                continue;
                            }
                        }
                    }

                    bool isConsonant = "ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎ".Contains(jamo);
                    if (isShiftPressed && isConsonant && EnhancedJamoMap.TryGetValue(jamo, out string? enhancedJamo))
                        jamoList.Add((enhancedJamo, isShiftPressed, position));
                    else
                        jamoList.Add((jamo, isShiftPressed, position));
                }
                else
                {
                    return string.Empty;
                }

                bufferIndex++;
            }

            List<string> result = new();
            JamoBuffer buffer = new();

            for (int i = 0; i < jamoList.Count; i++)
            {
                string current = jamoList[i].Jamo;
                bool isConsonant = "ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎㄲㄸㅃㅆㅉㄳㄵㄶㄺㄻㄼㄽㄾㄿㅀㅄ".Contains(current);
                bool isVowel = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ".Contains(current);

                if (current.Length == 1 && current[0] >= 0xAC00 && current[0] <= 0xD7A3)
                {
                    if (buffer.IsComplete || buffer.Choseong != null || buffer.Jungseong != null)
                    {
                        result.Add(buffer.ToHangul());
                        buffer = new();
                    }
                    result.Add(current);
                    continue;
                }

                if (current == " ")
                {
                    if (buffer.IsComplete)
                        result.Add(buffer.ToHangul());
                    else if (buffer.Choseong != null || buffer.Jungseong != null)
                        return string.Empty;
                    buffer = new();
                    result.Add(" ");
                    continue;
                }

                // ✅ 공중 자음 → 앞 글자의 종성으로 결합 가능한 경우
                if (i > 0 && buffer.Choseong == null && buffer.Jungseong == null && buffer.Jongseong == null)
                {
                    // 공중 자음 뒤에 모음이 온다면 → 새 음절 시작으로 판단하고 합성하지 않음
                    if (i + 1 < jamoList.Count &&
                        "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ".Contains(jamoList[i + 1].Jamo))
                    {
                        Debug.WriteLine($"공중 자음 '{current}'은 다음 모음 '{jamoList[i + 1].Jamo}'과 결합 예정 → 종성 합성 생략");
                    }
                    else
                    {
                        string previousChar = result.LastOrDefault();
                        if (!string.IsNullOrEmpty(previousChar) && previousChar.Length == 1)
                        {
                            char prev = previousChar[0];
                            if (prev >= 0xAC00 && prev <= 0xD7A3)
                            {
                                int unicode = prev - 0xAC00;
                                int cho = unicode / 588;
                                int jung = (unicode % 588) / 28;
                                int jong = (unicode % 28);

                                string[] jongSung = { "", "ㄱ", "ㄲ", "ㄳ", "ㄴ", "ㄵ", "ㄶ", "ㄷ", "ㄹ", "ㄺ", "ㄻ", "ㄼ", "ㄽ", "ㄾ", "ㄿ", "ㅀ", "ㅁ", "ㅂ", "ㅄ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ" };

                                string lastFinal = jongSung[jong];
                                string floatingConsonant = current;

                                string? combined = CombineFinalConsonants(lastFinal, floatingConsonant);
                                if (combined != null)
                                {
                                    int newJong = Array.IndexOf(jongSung, combined);
                                    if (newJong >= 0)
                                    {
                                        int newUnicode = cho * 588 + jung * 28 + newJong + 0xAC00;
                                        result[result.Count - 1] = ((char)newUnicode).ToString();
                                        Debug.WriteLine($"공중 자음 종성 합성: {lastFinal}+{floatingConsonant} → {combined}, 새 글자: {(char)newUnicode}");
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }

                if (isVowel)
                {
                    if (buffer.IsComplete)
                    {
                        result.Add(buffer.ToHangul());
                        buffer = new();
                    }
                    buffer.Jungseong = current;
                }
                else if (isConsonant)
                {
                    if (buffer.Choseong == null)
                    {
                        buffer.Choseong = current;
                    }
                    else if (buffer.IsComplete && buffer.Jongseong == null)
                    {
                        bool nextIsVowel = (i + 1 < jamoList.Count) &&
                            "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ".Contains(jamoList[i + 1].Jamo);

                        if (!nextIsVowel)
                        {
                            buffer.Jongseong = current;
                            result.Add(buffer.ToHangul());
                            buffer = new();
                        }
                        else
                        {
                            result.Add(buffer.ToHangul());
                            buffer = new() { Choseong = current };
                        }
                    }
                    else
                    {
                        if (buffer.Jungseong == null)
                            return string.Empty;
                        result.Add(buffer.ToHangul());
                        buffer = new() { Choseong = current };
                    }
                }
            }

            if (buffer.IsComplete)
                result.Add(buffer.ToHangul());
            else if (buffer.Choseong != null || buffer.Jungseong != null)
                return string.Empty;

            string koreanText = string.Join("", result).Trim();
            Debug.WriteLine($"영어 -> 한국어 변환: {inputText} -> {koreanText}");
            return koreanText;
        }


        private string? CombineVowels(string first, string second)
        {
            var vowelCombinations = new Dictionary<(string, string), string>
            {
                { ("ㅗ", "ㅏ"), "ㅘ" },
                { ("ㅗ", "ㅐ"), "ㅙ" },
                { ("ㅗ", "ㅣ"), "ㅚ" },
                { ("ㅜ", "ㅓ"), "ㅝ" },
                { ("ㅜ", "ㅔ"), "ㅞ" },
                { ("ㅜ", "ㅣ"), "ㅟ" },
                { ("ㅡ", "ㅣ"), "ㅢ" }
            };

            if (vowelCombinations.TryGetValue((first, second), out string? combined))
                return combined;
            return null;
        }

        private static string? CombineFinalConsonants(string first, string second)
        {
            var finalConsonantCombinations = new Dictionary<(string, string), string>
            {
                { ("ㄱ", "ㅅ"), "ㄳ" },
                { ("ㄴ", "ㅈ"), "ㄵ" },
                { ("ㄴ", "ㅎ"), "ㄶ" },
                { ("ㄹ", "ㄱ"), "ㄺ" },
                { ("ㄹ", "ㅁ"), "ㄻ" },
                { ("ㄹ", "ㅂ"), "ㄼ" },
                { ("ㄹ", "ㅅ"), "ㄽ" },
                { ("ㄹ", "ㅌ"), "ㄾ" },
                { ("ㄹ", "ㅍ"), "ㄿ" },
                { ("ㄹ", "ㅎ"), "ㅀ" },
                { ("ㅂ", "ㅅ"), "ㅄ" }
            };

            if (finalConsonantCombinations.TryGetValue((first, second), out string? combined))
                return combined;
            return null;
        }

        private class JamoBuffer
        {
            public string? Choseong { get; set; }
            public string? Jungseong { get; set; }
            public string? Jongseong { get; set; }

            public bool IsComplete => Choseong != null && Jungseong != null;
            public string ToHangul()
            {
                if (!IsComplete)
                    return ToPartialHangul();

                string[] choSung = { "ㄱ", "ㄲ", "ㄴ", "ㄷ", "ㄸ", "ㄹ", "ㅁ", "ㅂ", "ㅃ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅉ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ" };
                string[] jungSung = { "ㅏ", "ㅐ", "ㅑ", "ㅒ", "ㅓ", "ㅔ", "ㅕ", "ㅖ", "ㅗ", "ㅘ", "ㅙ", "ㅚ", "ㅛ", "ㅜ", "ㅝ", "ㅞ", "ㅟ", "ㅠ", "ㅡ", "ㅢ", "ㅣ" };
                string[] jongSung = { "", "ㄱ", "ㄲ", "ㄳ", "ㄴ", "ㄵ", "ㄶ", "ㄷ", "ㄹ", "ㄺ", "ㄻ", "ㄼ", "ㄽ", "ㄾ", "ㄿ", "ㅀ", "ㅁ", "ㅂ", "ㅄ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ" };

                string? finalJong = Jongseong;

                // ✅ 종성이 2글자라면 결합 시도
                if (!string.IsNullOrEmpty(Jongseong) && Jongseong.Length == 2)
                {
                    string first = Jongseong[0].ToString();
                    string second = Jongseong[1].ToString();
                    finalJong = CombineFinalConsonants(first, second) ?? Jongseong;
                }

                int choIndex = Array.IndexOf(choSung, Choseong);
                int jungIndex = Array.IndexOf(jungSung, Jungseong);
                int jongIndex = string.IsNullOrEmpty(finalJong) ? 0 : Array.IndexOf(jongSung, finalJong);

                if (choIndex >= 0 && jungIndex >= 0)
                {
                    int unicode = (choIndex * 588) + (jungIndex * 28) + jongIndex + 0xAC00;
                    return ((char)unicode).ToString();
                }

                return ToPartialHangul();
            }


            public string ToPartialHangul()
            {
                if (Choseong != null && Jungseong == null && Jongseong == null)
                    return Choseong;
                if (Jungseong != null && Choseong == null && Jongseong == null)
                    return Jungseong;
                if (Choseong != null && Jungseong != null)
                    return (Choseong ?? "") + (Jungseong ?? "") + (Jongseong ?? "");
                return string.Empty;
            }
        }

        private void UpdateStatusLabel(string input = "")
        {
            if (!isMonitoring)
            {
                if (IsHandleCreated)
                    statusLabel.Text = string.Empty;
                return;
            }

            string currentLanguage = isFirstEnter ? (isHangulMode ? "한국어" : "영어") : (isHangulMode ? "한국어" : "영어");
            string status = string.IsNullOrEmpty(input)
                ? $"대기 (입력기: {(isHangulMode ? "한글" : "영어")}, 현재 언어: {currentLanguage}, Shift: {isShiftPressed}, isFirstEnter: {isFirstEnter})"
                : $"{input} (입력기: {(isHangulMode ? "한글" : "영어")}, 현재 언어: {currentLanguage}, Shift: {isShiftPressed}, isFirstEnter: {isFirstEnter})";
            if (IsHandleCreated)
            {
                statusLabel.Text = status;
                Debug.WriteLine($"상태 업데이트: {status}, lastSegmentLanguage: {lastSegmentLanguage}");
            }
        }

        private void InitializeTTS()
        {
            try
            {
                synthesizer = new SpeechSynthesizer();
                var japaneseVoices = synthesizer.GetInstalledVoices(new System.Globalization.CultureInfo("ja-JP"));
                if (japaneseVoices.Count == 0 || !japaneseVoices[0].Enabled)
                {
                    Debug.WriteLine("일본어 TTS 음성 없음");
                    if (IsHandleCreated)
                    {
                        statusLabel.Text = "일본어 TTS 음성이 설치되지 않았습니다.";
                        startButton.Enabled = false;
                    }
                    synthesizer.Dispose();
                    synthesizer = new SpeechSynthesizer();
                    return;
                }
                synthesizer.SelectVoice(japaneseVoices[0].VoiceInfo.Name);
                synthesizer.SetOutputToDefaultAudioDevice();
                synthesizer.Volume = 100;
                synthesizer.Rate = speechRateTrackBar.Value;
                Debug.WriteLine($"TTS 초기화 완료: {japaneseVoices[0].VoiceInfo.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TTS 초기화 오류: {ex.Message}");
                if (IsHandleCreated)
                {
                    statusLabel.Text = $"TTS 초기화 오류: {ex.Message}";
                    startButton.Enabled = false;
                }
                synthesizer?.Dispose();
                synthesizer = new SpeechSynthesizer();
            }
        }

        private void LoadCredentialsFromIni()
        {
            try
            {
                if (File.Exists(IniFilePath))
                {
                    var lines = File.ReadAllLines(IniFilePath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("AzureApiKey=", StringComparison.OrdinalIgnoreCase))
                            azureApiKey = line.Substring("AzureApiKey=".Length).Trim();
                        else if (line.StartsWith("AzureRegion=", StringComparison.OrdinalIgnoreCase))
                            azureRegion = line.Substring("AzureRegion=".Length).Trim();
                    }
                    clientIdTextBox.Text = azureApiKey;
                    clientSecretTextBox.Text = azureRegion;
                    Debug.WriteLine("Azure API 키 및 리전 로드 완료");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Azure API 키 로드 오류: {ex.Message}");
            }
        }

        private void SaveCredentialsToIni()
        {
            try
            {
                var content = $"AzureApiKey={azureApiKey}\nAzureRegion={azureRegion}";
                File.WriteAllText(IniFilePath, content);
                Debug.WriteLine("Azure API 키 및 리전 저장 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Azure API 키 저장 오류: {ex.Message}");
            }
        }

        private void UpdateSpeechRate()
        {
            try
            {
                if (synthesizer != null)
                {
                    synthesizer.Rate = speechRateTrackBar.Value;
                    if (IsHandleCreated && isMonitoring)
                        statusLabel.Text = $"발음 속도 설정: {speechRateTrackBar.Value}, 현재 언어: {(isHangulMode ? "한국어" : "영어")}, Shift: {isShiftPressed}";
                    Debug.WriteLine($"TTS 속도 설정: {speechRateTrackBar.Value}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"음성 속도 업데이트 오류: {ex.Message}");
            }
        }

        private async void StartButton_Click(object? sender, EventArgs e)
        {
            if (!isMonitoring)
            {
                azureApiKey = clientIdTextBox?.Text.Trim() ?? string.Empty;
                azureRegion = clientSecretTextBox?.Text.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(azureApiKey) || string.IsNullOrEmpty(azureRegion))
                {
                    statusLabel.Text = "유효하지 않은 Azure API 키 또는 리전입니다.";
                    statusLabel.ForeColor = Color.Red;
                    Debug.WriteLine("유효하지 않은 API 정보로 시작 시도됨");
                    return;
                }

                try
                {
                    // 실제 API 테스트 요청
                    var testClient = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&from=ko&to=ja");
                    request.Headers.Add("Ocp-Apim-Subscription-Key", azureApiKey);
                    request.Headers.Add("Ocp-Apim-Subscription-Region", azureRegion);
                    var body = new[] { new { Text = "테스트" } };
                    request.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                    var response = await new HttpClient().SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    // API 테스트 성공 시 config 저장
                    SaveCredentialsToIni();

                    isMonitoring = true;
                    startButton.Text = "중지";
                    displayBuffer.Clear();
                    inputBuffer.Clear();
                    hangulKeyToggles.Clear();
                    isFirstEnter = true;
                    isHangulMode = false;
                    isShiftPressed = false;
                    lastSegmentLanguage = "English";
                    hangulKeyPressCountAfterLastKey = 0;
                    inputPositionCounter = 0;
                    ignoreHangulKeyUntilFirstSegment = true;
                    inputTextBox.Text = string.Empty;

                    statusLabel.ForeColor = Color.Black;
                    UpdateStatusLabel("입력 감지 시작");
                    Debug.WriteLine("입력 감지 시작");
                }
                catch (Exception ex)
                {
                    statusLabel.Text = "API 키 인증 실패 또는 네트워크 오류입니다.";
                    statusLabel.ForeColor = Color.Red;
                    Debug.WriteLine($"API 인증 실패: {ex.Message}");
                }
            }
            else
            {
                isMonitoring = false;
                startButton.Text = "시작";
                synthesizer?.SpeakAsyncCancelAll();
                statusLabel.Text = string.Empty;
                inputTimeoutTimer.Stop();
                ResetInputBuffers();
                Debug.WriteLine("입력 감지 중지");
            }
        }

        private void ConfirmButton_Click(object? sender, EventArgs e)
        {
            azureApiKey = clientIdTextBox?.Text.Trim() ?? string.Empty;
            azureRegion = clientSecretTextBox?.Text.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(azureApiKey) || string.IsNullOrEmpty(azureRegion))
            {
                statusLabel.Text = "Azure API 키와 리전을 입력해주세요.";
                Debug.WriteLine("Azure API 키 또는 리전 입력 누락");
                return;
            }

            SaveCredentialsToIni();
            statusLabel.Text = "Azure API 키 및 리전 저장 완료";
            Debug.WriteLine($"Azure API 키 및 리전 저장 완료");
        }

        private void SpeechRateTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            UpdateSpeechRate();
        }

        private async Task<string> TranslateToJapanese(string text)
        {
            try
            {
                string endpoint = "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&from=ko&to=ja";
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Ocp-Apim-Subscription-Key", azureApiKey);
                request.Headers.Add("Ocp-Apim-Subscription-Region", azureRegion);
                var body = new[] { new { Text = text } };
                request.Content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(body),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                var json = JArray.Parse(responseBody);
                string translatedText = json[0]["translations"][0]["text"]?.ToString() ?? string.Empty;
                Debug.WriteLine($"번역 성공: {text} -> {translatedText}");
                return translatedText;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"일본어 번역 오류: {ex.Message}");
                throw;
            }
        }
        private void CleanupResources()
        {
            try
            {
                if (keyboardHookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(keyboardHookId);
                    keyboardHookId = IntPtr.Zero;
                    Debug.WriteLine("키보드 훅 해제 완료");
                }
                if (shellHookId != IntPtr.Zero)
                {
                    DeregisterShellHookWindow(shellHookId);
                    shellHookId = IntPtr.Zero;
                    Debug.WriteLine("쉘 훅 해제 완료");
                }
                inputTimeoutTimer?.Dispose();
                synthesizer?.Dispose();
                httpClient?.Dispose();
                isShiftPressed = false;
                Debug.WriteLine("자원 정리 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"자원 정리 오류: {ex.Message}");
            }
        }
    }
}