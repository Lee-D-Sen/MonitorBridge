using System.Globalization;
using System.Windows.Markup;
using MonitorBridge.Core;

namespace MonitorBridge.App.Services;

/// <summary>
/// UI 문자열 번역. 키가 없는 언어는 영어로, 영어에도 없으면 키를 그대로 보여준다.
/// 기본 언어는 Windows 표시 언어를 따르고, 지원하지 않는 언어는 영어다. 번역이 어색하면 이 파일의 해당 표만 고치면 된다.
/// </summary>
internal static class Loc
{
    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"), ("ko", "한국어"), ("zh", "简体中文"), ("hi", "हिन्दी"), ("ja", "日本語"), ("es", "Español"), ("de", "Deutsch"),
    };

    public static string Lang { get; private set; } = "en";

    /// <summary>저장된 설정이 유효하면 그것을, 아니면 Windows 표시 언어(지원 언어일 때), 그 외엔 영어.</summary>
    public static string Resolve(string? saved)
    {
        if (saved is not null && Languages.Any(l => l.Code == saved)) return saved;
        var c = CultureInfo.CurrentUICulture;
        string two = c.TwoLetterISOLanguageName.ToLowerInvariant();
        if (two == "zh")
        {
            // 간체(zh-CN, zh-SG, zh-Hans)만 지원. 번체(zh-TW/HK/MO, zh-Hant)는 영어로 시작하고 창에서 바꿀 수 있다.
            string n = c.Name;
            bool hant = n.Contains("Hant", StringComparison.OrdinalIgnoreCase) || n.EndsWith("TW") || n.EndsWith("HK") || n.EndsWith("MO");
            return hant ? "en" : "zh";
        }
        return Languages.Any(l => l.Code == two) ? two : "en";
    }

    public static void Set(string? lang)
    {
        Lang = Languages.Any(l => l.Code == lang) ? lang! : "en";
        CoreText.Lookup = (key, args) => T(key, args);
    }

    public static string T(string key, params object[] args)
    {
        string? s = null;
        if (!(Tables.TryGetValue(Lang, out var table) && table.TryGetValue(key, out s)) && !En.TryGetValue(key, out s)) s = key;
        return args.Length == 0 ? s : string.Format(CultureInfo.CurrentCulture, s, args);
    }

    // ================= English (기준) =================
    private static readonly Dictionary<string, string> En = new()
    {
        ["app.title"] = "MonitorBridge — Multi-monitor cursor correction",
        ["app.error"] = "An unexpected error occurred.\n{0}",
        ["tray.hint"] = "Closing the window keeps it running in the tray.",
        ["tray.open"] = "Open window",
        ["tray.toggle"] = "Turn correction on/off",
        ["tray.exit"] = "Exit",
        ["tray.on"] = "Turn correction off",
        ["tray.off"] = "Turn correction on",
        ["tray.text"] = "MonitorBridge — correction {0}",
        ["state.on"] = "on",
        ["state.suspended"] = "paused",
        ["state.off"] = "off",

        ["sel.title"] = "Selected monitor",
        ["sel.source"] = "Size source: {0}",
        ["size.title"] = "Physical screen size",
        ["size.diag"] = "Diagonal",
        ["size.inch"] = "in",
        ["size.w"] = "Width",
        ["size.h"] = "Height",
        ["pos.title"] = "Position on desk (top-left corner)",
        ["pos.x"] = "Horizontal X",
        ["pos.y"] = "Vertical Y",
        ["pos.hint"] = "Y increases downward. Equal Y on two monitors aligns their top edges; equal Y + height aligns their bottom edges.",
        ["align.title"] = "Align to neighbor",
        ["align.start"] = "Top/Left",
        ["align.center"] = "Center",
        ["align.end"] = "Bottom/Right",
        ["help.drag"] = "Dragging snaps to other monitors' edges (hold Alt to disable). Windows scaling is not used as physical size; if the monitor info could not be read, a 24-inch estimate is used, so please enter the real size.",
        ["btn.identify"] = "Identify screens",
        ["btn.redetect"] = "Re-detect",
        ["btn.reset"] = "Reset to defaults",
        ["btn.apply"] = "Apply",
        ["btn.on"] = "Turn on",
        ["btn.off"] = "Turn off",
        ["chk.startup"] = "Start in tray with Windows",
        ["lang.label"] = "Language",

        ["banner.on"] = "● Correction on",
        ["banner.on.detail"] = "{0} monitors · {1} crossable edge segment(s)",
        ["banner.on.dirty"] = "  —  Press 'Apply' to use your edits.",
        ["banner.susp"] = "▲ Correction paused (Windows default behavior)",
        ["banner.susp.detail"] = "Re-checking the display configuration.",
        ["banner.off"] = "○ Correction off (Windows default behavior)",
        ["banner.off.one"] = "Only 1 monitor detected. Connect 2 or more to use correction.",
        ["banner.off.many"] = "Check that the green segments match your real desk layout, then press 'Apply' to turn it on.",

        ["dlg.apply.fail"] = "Cannot apply",
        ["dlg.reset.text"] = "This clears the saved layout and returns to defaults based on the Windows arrangement.\nCorrection will be turned off; press 'Apply' to turn it on again.\n(Windows display settings are not changed.)",
        ["dlg.reset.title"] = "Reset to defaults",
        ["dlg.startup.fail"] = "Could not change the startup setting.\n{0}",
        ["dlg.error"] = "Error",

        ["canvas.legend"] = "■ Green line: where the cursor can cross (other edges are blocked)",

        ["vm.title"] = "#{0} · {1}",
        ["vm.detail"] = "{0}×{1} · scale {2}% · {3}",
        ["vm.landscape"] = "Landscape",
        ["vm.portrait90"] = "Portrait (90°)",
        ["vm.landscape180"] = "Landscape (flipped)",
        ["vm.portrait270"] = "Portrait (270°)",
        ["vm.src.user"] = "Entered / estimated",
        ["vm.src.edid"] = "Monitor info (EDID)",

        ["ctl.one"] = "Only one monitor is present, so correction is not needed. Correction was paused.",
        ["ctl.mismatch"] = "{0} Correction was turned off for safety. Check the layout and press 'Apply' to turn it on again.",
        ["ctl.mismatch.default"] = "The saved monitors differ from the current ones.",
        ["ctl.overlap.run"] = "Monitors overlap in the layout (this can happen after a rotation or resolution change). Adjust the layout and press 'Apply'.",
        ["ctl.need2"] = "Correction needs 2 or more monitors.",
        ["ctl.badsize"] = "Invalid screen size (width 100–3000 mm, height 60–3000 mm).",
        ["ctl.overlap.apply"] = "Monitors overlap on the desk. Please arrange them so they do not overlap.",
        ["hook.fail"] = "Could not install the mouse hook.",

        ["core.nosaved"] = "No saved settings.",
        ["core.count"] = "The number of monitors differs from the saved settings (saved {0}, now {1}).",
        ["core.nomatch"] = "Cannot safely match the saved monitors to the current ones (a monitor was replaced or its port changed).",
    };

    // ================= 한국어 =================
    private static readonly Dictionary<string, string> Ko = new()
    {
        ["app.title"] = "MonitorBridge — 멀티 모니터 커서 보정",
        ["app.error"] = "예기치 않은 오류가 발생했습니다.\n{0}",
        ["tray.hint"] = "창을 닫아도 트레이에서 계속 동작합니다.",
        ["tray.open"] = "창 열기",
        ["tray.toggle"] = "보정 켜기/끄기",
        ["tray.exit"] = "종료",
        ["tray.on"] = "보정 끄기",
        ["tray.off"] = "보정 켜기",
        ["tray.text"] = "MonitorBridge — 보정 {0}",
        ["state.on"] = "켜짐",
        ["state.suspended"] = "일시 중지",
        ["state.off"] = "꺼짐",

        ["sel.title"] = "선택한 모니터",
        ["sel.source"] = "크기 출처: {0}",
        ["size.title"] = "실제 화면 크기",
        ["size.diag"] = "대각선",
        ["size.inch"] = "인치",
        ["size.w"] = "가로 길이",
        ["size.h"] = "세로 길이",
        ["pos.title"] = "책상 위 위치 (왼쪽 위 모서리)",
        ["pos.x"] = "가로 위치 X",
        ["pos.y"] = "높이 위치 Y",
        ["pos.hint"] = "Y는 아래쪽이 +입니다. 두 모니터의 Y를 같게 하면 윗선이, Y+세로를 같게 하면 아랫선이 같은 높이가 됩니다.",
        ["align.title"] = "이웃 모니터에 맞춤",
        ["align.start"] = "윗/왼쪽선",
        ["align.center"] = "가운데",
        ["align.end"] = "아랫/오른쪽선",
        ["help.drag"] = "드래그로 옮기면 다른 모니터 가장자리에 붙습니다(Alt를 누르면 해제). Windows 배율은 실제 크기로 쓰지 않으며, 모니터 정보를 못 읽은 경우 24인치 추정값이니 꼭 직접 입력해 주세요.",
        ["btn.identify"] = "화면 식별",
        ["btn.redetect"] = "다시 감지",
        ["btn.reset"] = "기본값으로 되돌리기",
        ["btn.apply"] = "적용",
        ["btn.on"] = "보정 켜기",
        ["btn.off"] = "보정 끄기",
        ["chk.startup"] = "Windows 시작 시 트레이로 실행",
        ["lang.label"] = "언어",

        ["banner.on"] = "● 보정 켜짐",
        ["banner.on.detail"] = "모니터 {0}대 · 통과 가능한 경계 구간 {1}곳",
        ["banner.on.dirty"] = "  —  편집한 내용은 '적용'을 눌러야 반영됩니다.",
        ["banner.susp"] = "▲ 보정 일시 중지 (Windows 기본 동작)",
        ["banner.susp.detail"] = "화면 구성을 다시 확인하는 중입니다.",
        ["banner.off"] = "○ 보정 꺼짐 (Windows 기본 동작)",
        ["banner.off.one"] = "모니터가 1대로 감지되었습니다. 2대 이상 연결하면 보정할 수 있습니다.",
        ["banner.off.many"] = "초록 선 구간이 실제 책상 위 배치와 맞는지 확인한 뒤 '적용'을 누르면 켜집니다.",

        ["dlg.apply.fail"] = "적용할 수 없습니다",
        ["dlg.reset.text"] = "저장된 배치를 지우고 Windows 배치 기준 기본값으로 되돌립니다.\n보정은 꺼지며, 다시 켜려면 '적용'을 눌러야 합니다.\n(Windows 디스플레이 설정은 바뀌지 않습니다.)",
        ["dlg.reset.title"] = "기본값으로 되돌리기",
        ["dlg.startup.fail"] = "시작 프로그램 설정을 바꾸지 못했습니다.\n{0}",
        ["dlg.error"] = "오류",

        ["canvas.legend"] = "■ 초록 선: 커서가 넘나드는 구간 (그 밖의 경계는 막힘)",

        ["vm.title"] = "{0}번 · {1}",
        ["vm.detail"] = "{0}×{1} · 배율 {2}% · {3}",
        ["vm.landscape"] = "가로",
        ["vm.portrait90"] = "세로(90°)",
        ["vm.landscape180"] = "가로(뒤집힘)",
        ["vm.portrait270"] = "세로(270°)",
        ["vm.src.user"] = "직접 입력/추정값",
        ["vm.src.edid"] = "모니터 정보(EDID)",

        ["ctl.one"] = "모니터가 1대뿐이어서 보정이 필요 없습니다. 보정을 일시 중지했습니다.",
        ["ctl.mismatch"] = "{0} 안전을 위해 보정을 껐습니다. 배치를 확인하고 '적용'을 눌러 다시 켜세요.",
        ["ctl.mismatch.default"] = "저장된 모니터와 현재 모니터가 다릅니다.",
        ["ctl.overlap.run"] = "모니터 배치가 서로 겹쳐 있습니다(회전 또는 해상도 변경 후일 수 있음). 배치를 조정하고 '적용'을 눌러 주세요.",
        ["ctl.need2"] = "모니터가 2대 이상일 때 보정할 수 있습니다.",
        ["ctl.badsize"] = "화면 크기가 올바르지 않습니다(가로 100~3000mm, 세로 60~3000mm).",
        ["ctl.overlap.apply"] = "모니터가 책상 위에서 서로 겹쳐 있습니다. 겹치지 않게 배치해 주세요.",
        ["hook.fail"] = "마우스 훅을 설치하지 못했습니다.",

        ["core.nosaved"] = "저장된 설정이 없습니다.",
        ["core.count"] = "모니터 수가 저장된 설정({0}대)과 다릅니다(현재 {1}대).",
        ["core.nomatch"] = "저장된 모니터와 현재 모니터를 안전하게 연결할 수 없습니다(모니터가 바뀌었거나 연결 포트가 달라졌습니다).",
    };

    // ================= 简体中文 =================
    private static readonly Dictionary<string, string> Zh = new()
    {
        ["app.title"] = "MonitorBridge — 多显示器光标校正",
        ["app.error"] = "发生意外错误。\n{0}",
        ["tray.hint"] = "关闭窗口后，程序仍会在系统托盘中运行。",
        ["tray.open"] = "打开窗口",
        ["tray.toggle"] = "开启/关闭校正",
        ["tray.exit"] = "退出",
        ["tray.on"] = "关闭校正",
        ["tray.off"] = "开启校正",
        ["tray.text"] = "MonitorBridge — 校正{0}",
        ["state.on"] = "已开启",
        ["state.suspended"] = "已暂停",
        ["state.off"] = "已关闭",

        ["sel.title"] = "所选显示器",
        ["sel.source"] = "尺寸来源：{0}",
        ["size.title"] = "实际屏幕尺寸",
        ["size.diag"] = "对角线",
        ["size.inch"] = "英寸",
        ["size.w"] = "宽度",
        ["size.h"] = "高度",
        ["pos.title"] = "桌面上的位置（左上角）",
        ["pos.x"] = "水平位置 X",
        ["pos.y"] = "垂直位置 Y",
        ["pos.hint"] = "Y 向下为正。两台显示器的 Y 相同则上边对齐；Y 加高度相同则下边对齐。",
        ["align.title"] = "对齐相邻显示器",
        ["align.start"] = "上/左边",
        ["align.center"] = "居中",
        ["align.end"] = "下/右边",
        ["help.drag"] = "拖动时会吸附到其他显示器的边缘（按住 Alt 可取消）。Windows 缩放比例不会作为实际尺寸使用；如果无法读取显示器信息，将使用 24 英寸的估算值，请务必手动输入真实尺寸。",
        ["btn.identify"] = "识别屏幕",
        ["btn.redetect"] = "重新检测",
        ["btn.reset"] = "恢复默认设置",
        ["btn.apply"] = "应用",
        ["btn.on"] = "开启",
        ["btn.off"] = "关闭",
        ["chk.startup"] = "随 Windows 启动并最小化到托盘",
        ["lang.label"] = "语言",

        ["banner.on"] = "● 校正已开启",
        ["banner.on.detail"] = "{0} 台显示器 · {1} 段可穿越的边缘",
        ["banner.on.dirty"] = "  —  请点击“应用”使修改生效。",
        ["banner.susp"] = "▲ 校正已暂停（Windows 默认行为）",
        ["banner.susp.detail"] = "正在重新检查显示器配置。",
        ["banner.off"] = "○ 校正已关闭（Windows 默认行为）",
        ["banner.off.one"] = "仅检测到 1 台显示器。连接 2 台或以上后即可使用校正。",
        ["banner.off.many"] = "请确认绿色线段与桌面上的实际布局一致，然后点击“应用”开启。",

        ["dlg.apply.fail"] = "无法应用",
        ["dlg.reset.text"] = "这将清除已保存的布局，并根据 Windows 的排列恢复默认值。\n校正将被关闭；如需再次开启，请点击“应用”。\n（不会更改 Windows 显示设置。）",
        ["dlg.reset.title"] = "恢复默认设置",
        ["dlg.startup.fail"] = "无法更改开机启动设置。\n{0}",
        ["dlg.error"] = "错误",

        ["canvas.legend"] = "■ 绿线：光标可穿越的位置（其他边缘会被阻挡）",

        ["vm.title"] = "{0} 号 · {1}",
        ["vm.detail"] = "{0}×{1} · 缩放 {2}% · {3}",
        ["vm.landscape"] = "横向",
        ["vm.portrait90"] = "纵向（90°）",
        ["vm.landscape180"] = "横向（翻转）",
        ["vm.portrait270"] = "纵向（270°）",
        ["vm.src.user"] = "手动输入/估算值",
        ["vm.src.edid"] = "显示器信息（EDID）",

        ["ctl.one"] = "只有一台显示器，无需校正。已暂停校正。",
        ["ctl.mismatch"] = "{0} 为了安全，已关闭校正。请检查布局，然后点击“应用”重新开启。",
        ["ctl.mismatch.default"] = "已保存的显示器与当前显示器不一致。",
        ["ctl.overlap.run"] = "布局中的显示器相互重叠（可能是旋转或更改分辨率之后）。请调整布局并点击“应用”。",
        ["ctl.need2"] = "需要 2 台或以上显示器才能使用校正。",
        ["ctl.badsize"] = "屏幕尺寸无效（宽度 100–3000 mm，高度 60–3000 mm）。",
        ["ctl.overlap.apply"] = "显示器在桌面上相互重叠。请调整位置，使其不重叠。",
        ["hook.fail"] = "无法安装鼠标钩子。",

        ["core.nosaved"] = "没有已保存的设置。",
        ["core.count"] = "显示器数量与已保存的设置不一致（已保存 {0} 台，当前 {1} 台）。",
        ["core.nomatch"] = "无法安全地将已保存的显示器与当前显示器对应起来（显示器已更换或连接端口已改变）。",
    };

    // ================= हिन्दी =================
    private static readonly Dictionary<string, string> Hi = new()
    {
        ["app.title"] = "MonitorBridge — मल्टी-मॉनिटर कर्सर सुधार",
        ["app.error"] = "एक अप्रत्याशित त्रुटि हुई।\n{0}",
        ["tray.hint"] = "विंडो बंद करने पर भी ऐप ट्रे में चलता रहता है।",
        ["tray.open"] = "विंडो खोलें",
        ["tray.toggle"] = "सुधार चालू/बंद करें",
        ["tray.exit"] = "बाहर निकलें",
        ["tray.on"] = "सुधार बंद करें",
        ["tray.off"] = "सुधार चालू करें",
        ["tray.text"] = "MonitorBridge — सुधार {0}",
        ["state.on"] = "चालू",
        ["state.suspended"] = "रुका हुआ",
        ["state.off"] = "बंद",

        ["sel.title"] = "चुना गया मॉनिटर",
        ["sel.source"] = "आकार का स्रोत: {0}",
        ["size.title"] = "वास्तविक स्क्रीन आकार",
        ["size.diag"] = "विकर्ण",
        ["size.inch"] = "इंच",
        ["size.w"] = "चौड़ाई",
        ["size.h"] = "ऊँचाई",
        ["pos.title"] = "डेस्क पर स्थिति (ऊपरी-बायाँ कोना)",
        ["pos.x"] = "क्षैतिज X",
        ["pos.y"] = "ऊर्ध्वाधर Y",
        ["pos.hint"] = "Y नीचे की ओर बढ़ता है। दो मॉनिटरों का Y समान रखने पर ऊपरी किनारे बराबर होते हैं; Y + ऊँचाई समान रखने पर निचले किनारे बराबर होते हैं।",
        ["align.title"] = "पड़ोसी मॉनिटर से मिलाएँ",
        ["align.start"] = "ऊपर/बायाँ",
        ["align.center"] = "केंद्र",
        ["align.end"] = "नीचे/दायाँ",
        ["help.drag"] = "खींचने पर मॉनिटर दूसरे मॉनिटरों के किनारों से चिपक जाता है (बंद करने के लिए Alt दबाए रखें)। Windows स्केलिंग को वास्तविक आकार नहीं माना जाता; यदि मॉनिटर की जानकारी नहीं पढ़ी जा सकी, तो 24 इंच का अनुमान लिया जाता है, इसलिए कृपया वास्तविक आकार स्वयं दर्ज करें।",
        ["btn.identify"] = "स्क्रीन पहचानें",
        ["btn.redetect"] = "फिर से खोजें",
        ["btn.reset"] = "डिफ़ॉल्ट पर लौटें",
        ["btn.apply"] = "लागू करें",
        ["btn.on"] = "चालू करें",
        ["btn.off"] = "बंद करें",
        ["chk.startup"] = "Windows के साथ ट्रे में शुरू करें",
        ["lang.label"] = "भाषा",

        ["banner.on"] = "● सुधार चालू है",
        ["banner.on.detail"] = "{0} मॉनिटर · पार करने योग्य {1} किनारे के खंड",
        ["banner.on.dirty"] = "  —  अपने बदलाव उपयोग करने के लिए 'लागू करें' दबाएँ।",
        ["banner.susp"] = "▲ सुधार रुका हुआ है (Windows का डिफ़ॉल्ट व्यवहार)",
        ["banner.susp.detail"] = "डिस्प्ले कॉन्फ़िगरेशन दोबारा जाँचा जा रहा है।",
        ["banner.off"] = "○ सुधार बंद है (Windows का डिफ़ॉल्ट व्यवहार)",
        ["banner.off.one"] = "केवल 1 मॉनिटर मिला। सुधार का उपयोग करने के लिए 2 या अधिक मॉनिटर जोड़ें।",
        ["banner.off.many"] = "जाँचें कि हरे खंड आपके डेस्क की वास्तविक व्यवस्था से मेल खाते हैं, फिर चालू करने के लिए 'लागू करें' दबाएँ।",

        ["dlg.apply.fail"] = "लागू नहीं किया जा सका",
        ["dlg.reset.text"] = "इससे सहेजी गई व्यवस्था हट जाएगी और Windows की व्यवस्था के आधार पर डिफ़ॉल्ट मान लौट आएँगे।\nसुधार बंद हो जाएगा; इसे फिर चालू करने के लिए 'लागू करें' दबाएँ।\n(Windows डिस्प्ले सेटिंग्स नहीं बदली जाएँगी।)",
        ["dlg.reset.title"] = "डिफ़ॉल्ट पर लौटें",
        ["dlg.startup.fail"] = "स्टार्टअप सेटिंग नहीं बदली जा सकी।\n{0}",
        ["dlg.error"] = "त्रुटि",

        ["canvas.legend"] = "■ हरी रेखा: जहाँ कर्सर पार कर सकता है (अन्य किनारे बंद हैं)",

        ["vm.title"] = "#{0} · {1}",
        ["vm.detail"] = "{0}×{1} · स्केल {2}% · {3}",
        ["vm.landscape"] = "लैंडस्केप",
        ["vm.portrait90"] = "पोर्ट्रेट (90°)",
        ["vm.landscape180"] = "लैंडस्केप (उलटा)",
        ["vm.portrait270"] = "पोर्ट्रेट (270°)",
        ["vm.src.user"] = "स्वयं दर्ज / अनुमानित",
        ["vm.src.edid"] = "मॉनिटर जानकारी (EDID)",

        ["ctl.one"] = "केवल एक मॉनिटर है, इसलिए सुधार की आवश्यकता नहीं है। सुधार रोक दिया गया।",
        ["ctl.mismatch"] = "{0} सुरक्षा के लिए सुधार बंद कर दिया गया। व्यवस्था जाँचें और फिर चालू करने के लिए 'लागू करें' दबाएँ।",
        ["ctl.mismatch.default"] = "सहेजे गए मॉनिटर वर्तमान मॉनिटरों से अलग हैं।",
        ["ctl.overlap.run"] = "व्यवस्था में मॉनिटर एक-दूसरे पर चढ़े हुए हैं (घुमाने या रिज़ॉल्यूशन बदलने के बाद ऐसा हो सकता है)। व्यवस्था ठीक करें और 'लागू करें' दबाएँ।",
        ["ctl.need2"] = "सुधार के लिए 2 या अधिक मॉनिटर आवश्यक हैं।",
        ["ctl.badsize"] = "स्क्रीन का आकार अमान्य है (चौड़ाई 100–3000 मिमी, ऊँचाई 60–3000 मिमी)।",
        ["ctl.overlap.apply"] = "डेस्क पर मॉनिटर एक-दूसरे पर चढ़े हुए हैं। कृपया उन्हें इस तरह रखें कि वे न चढ़ें।",
        ["hook.fail"] = "माउस हुक स्थापित नहीं किया जा सका।",

        ["core.nosaved"] = "कोई सहेजी गई सेटिंग नहीं है।",
        ["core.count"] = "मॉनिटरों की संख्या सहेजी गई सेटिंग से अलग है (सहेजे गए {0}, अभी {1})।",
        ["core.nomatch"] = "सहेजे गए मॉनिटरों को वर्तमान मॉनिटरों से सुरक्षित रूप से मिलाया नहीं जा सका (कोई मॉनिटर बदला गया या उसका पोर्ट बदल गया)।",
    };

    // ================= 日本語 =================
    private static readonly Dictionary<string, string> Ja = new()
    {
        ["app.title"] = "MonitorBridge — マルチモニターのカーソル補正",
        ["app.error"] = "予期しないエラーが発生しました。\n{0}",
        ["tray.hint"] = "ウィンドウを閉じてもタスクトレイで動作し続けます。",
        ["tray.open"] = "ウィンドウを開く",
        ["tray.toggle"] = "補正のオン/オフ",
        ["tray.exit"] = "終了",
        ["tray.on"] = "補正をオフにする",
        ["tray.off"] = "補正をオンにする",
        ["tray.text"] = "MonitorBridge — 補正 {0}",
        ["state.on"] = "オン",
        ["state.suspended"] = "一時停止",
        ["state.off"] = "オフ",

        ["sel.title"] = "選択中のモニター",
        ["sel.source"] = "サイズの取得元: {0}",
        ["size.title"] = "実際の画面サイズ",
        ["size.diag"] = "対角",
        ["size.inch"] = "インチ",
        ["size.w"] = "幅",
        ["size.h"] = "高さ",
        ["pos.title"] = "机上の位置（左上の角）",
        ["pos.x"] = "横位置 X",
        ["pos.y"] = "縦位置 Y",
        ["pos.hint"] = "Y は下方向がプラスです。2 台の Y を同じにすると上端が、Y＋高さを同じにすると下端がそろいます。",
        ["align.title"] = "隣のモニターに合わせる",
        ["align.start"] = "上/左端",
        ["align.center"] = "中央",
        ["align.end"] = "下/右端",
        ["help.drag"] = "ドラッグすると他のモニターの端に吸着します（Alt キーで解除）。Windows の拡大率は実寸として使いません。モニター情報を読み取れない場合は 24 インチの推定値になるため、必ず実際のサイズを入力してください。",
        ["btn.identify"] = "画面を識別",
        ["btn.redetect"] = "再検出",
        ["btn.reset"] = "初期値に戻す",
        ["btn.apply"] = "適用",
        ["btn.on"] = "オンにする",
        ["btn.off"] = "オフにする",
        ["chk.startup"] = "Windows 起動時にトレイで開始",
        ["lang.label"] = "言語",

        ["banner.on"] = "● 補正オン",
        ["banner.on.detail"] = "モニター {0} 台 · 通過できる境界 {1} 区間",
        ["banner.on.dirty"] = "  —  編集内容を反映するには「適用」を押してください。",
        ["banner.susp"] = "▲ 補正を一時停止中（Windows の標準動作）",
        ["banner.susp.detail"] = "ディスプレイ構成を再確認しています。",
        ["banner.off"] = "○ 補正オフ（Windows の標準動作）",
        ["banner.off.one"] = "モニターが 1 台だけ検出されました。2 台以上接続すると補正を使えます。",
        ["banner.off.many"] = "緑の線が実際の机上の配置と合っているか確認し、「適用」を押すとオンになります。",

        ["dlg.apply.fail"] = "適用できません",
        ["dlg.reset.text"] = "保存した配置を消去し、Windows の配置に基づく初期値に戻します。\n補正はオフになります。再びオンにするには「適用」を押してください。\n（Windows のディスプレイ設定は変更されません。）",
        ["dlg.reset.title"] = "初期値に戻す",
        ["dlg.startup.fail"] = "スタートアップの設定を変更できませんでした。\n{0}",
        ["dlg.error"] = "エラー",

        ["canvas.legend"] = "■ 緑の線: カーソルが通過できる区間（それ以外の境界は通れません）",

        ["vm.title"] = "{0} 番 · {1}",
        ["vm.detail"] = "{0}×{1} · 拡大率 {2}% · {3}",
        ["vm.landscape"] = "横向き",
        ["vm.portrait90"] = "縦向き（90°）",
        ["vm.landscape180"] = "横向き（反転）",
        ["vm.portrait270"] = "縦向き（270°）",
        ["vm.src.user"] = "手動入力/推定値",
        ["vm.src.edid"] = "モニター情報（EDID）",

        ["ctl.one"] = "モニターが 1 台のみのため、補正は不要です。補正を一時停止しました。",
        ["ctl.mismatch"] = "{0} 安全のため補正をオフにしました。配置を確認し、「適用」を押して再びオンにしてください。",
        ["ctl.mismatch.default"] = "保存されたモニターと現在のモニターが異なります。",
        ["ctl.overlap.run"] = "配置上でモニターが重なっています（回転や解像度の変更後に起こることがあります）。配置を調整して「適用」を押してください。",
        ["ctl.need2"] = "補正にはモニターが 2 台以上必要です。",
        ["ctl.badsize"] = "画面サイズが正しくありません（幅 100〜3000 mm、高さ 60〜3000 mm）。",
        ["ctl.overlap.apply"] = "机上でモニターが重なっています。重ならないように配置してください。",
        ["hook.fail"] = "マウスフックを設定できませんでした。",

        ["core.nosaved"] = "保存された設定がありません。",
        ["core.count"] = "モニターの台数が保存された設定と異なります（保存 {0} 台、現在 {1} 台）。",
        ["core.nomatch"] = "保存されたモニターと現在のモニターを安全に対応付けできません（モニターが交換されたか、接続ポートが変わりました）。",
    };

    // ================= Español =================
    private static readonly Dictionary<string, string> Es = new()
    {
        ["app.title"] = "MonitorBridge — Corrección del cursor en varios monitores",
        ["app.error"] = "Se produjo un error inesperado.\n{0}",
        ["tray.hint"] = "Al cerrar la ventana, la aplicación sigue funcionando en la bandeja.",
        ["tray.open"] = "Abrir ventana",
        ["tray.toggle"] = "Activar/desactivar corrección",
        ["tray.exit"] = "Salir",
        ["tray.on"] = "Desactivar corrección",
        ["tray.off"] = "Activar corrección",
        ["tray.text"] = "MonitorBridge — corrección {0}",
        ["state.on"] = "activada",
        ["state.suspended"] = "en pausa",
        ["state.off"] = "desactivada",

        ["sel.title"] = "Monitor seleccionado",
        ["sel.source"] = "Origen del tamaño: {0}",
        ["size.title"] = "Tamaño físico de la pantalla",
        ["size.diag"] = "Diagonal",
        ["size.inch"] = "pulg.",
        ["size.w"] = "Ancho",
        ["size.h"] = "Alto",
        ["pos.title"] = "Posición en el escritorio (esquina superior izquierda)",
        ["pos.x"] = "Horizontal X",
        ["pos.y"] = "Vertical Y",
        ["pos.hint"] = "Y aumenta hacia abajo. Si dos monitores tienen la misma Y, sus bordes superiores quedan alineados; si tienen la misma Y + alto, quedan alineados sus bordes inferiores.",
        ["align.title"] = "Alinear con el monitor vecino",
        ["align.start"] = "Arriba/Izq.",
        ["align.center"] = "Centro",
        ["align.end"] = "Abajo/Der.",
        ["help.drag"] = "Al arrastrar, el monitor se ajusta a los bordes de otros monitores (mantén Alt para desactivarlo). La escala de Windows no se usa como tamaño físico; si no se pudo leer la información del monitor, se usa una estimación de 24 pulgadas, así que introduce el tamaño real.",
        ["btn.identify"] = "Identificar pantallas",
        ["btn.redetect"] = "Detectar de nuevo",
        ["btn.reset"] = "Restablecer valores",
        ["btn.apply"] = "Aplicar",
        ["btn.on"] = "Activar",
        ["btn.off"] = "Desactivar",
        ["chk.startup"] = "Iniciar en la bandeja con Windows",
        ["lang.label"] = "Idioma",

        ["banner.on"] = "● Corrección activada",
        ["banner.on.detail"] = "{0} monitores · {1} tramo(s) de borde que se puede(n) cruzar",
        ["banner.on.dirty"] = "  —  Pulsa «Aplicar» para usar tus cambios.",
        ["banner.susp"] = "▲ Corrección en pausa (comportamiento predeterminado de Windows)",
        ["banner.susp.detail"] = "Comprobando de nuevo la configuración de pantallas.",
        ["banner.off"] = "○ Corrección desactivada (comportamiento predeterminado de Windows)",
        ["banner.off.one"] = "Solo se detectó 1 monitor. Conecta 2 o más para usar la corrección.",
        ["banner.off.many"] = "Comprueba que los tramos verdes coinciden con la disposición real de tu escritorio y pulsa «Aplicar» para activarla.",

        ["dlg.apply.fail"] = "No se puede aplicar",
        ["dlg.reset.text"] = "Se borrará la disposición guardada y se volverá a los valores predeterminados según la disposición de Windows.\nLa corrección se desactivará; pulsa «Aplicar» para activarla de nuevo.\n(La configuración de pantalla de Windows no se modifica.)",
        ["dlg.reset.title"] = "Restablecer valores",
        ["dlg.startup.fail"] = "No se pudo cambiar la opción de inicio.\n{0}",
        ["dlg.error"] = "Error",

        ["canvas.legend"] = "■ Línea verde: por donde el cursor puede cruzar (los demás bordes están bloqueados)",

        ["vm.title"] = "N.º {0} · {1}",
        ["vm.detail"] = "{0}×{1} · escala {2}% · {3}",
        ["vm.landscape"] = "Horizontal",
        ["vm.portrait90"] = "Vertical (90°)",
        ["vm.landscape180"] = "Horizontal (invertido)",
        ["vm.portrait270"] = "Vertical (270°)",
        ["vm.src.user"] = "Introducido/estimado",
        ["vm.src.edid"] = "Información del monitor (EDID)",

        ["ctl.one"] = "Solo hay un monitor, por lo que no hace falta la corrección. Se ha puesto en pausa.",
        ["ctl.mismatch"] = "{0} Por seguridad se desactivó la corrección. Revisa la disposición y pulsa «Aplicar» para activarla de nuevo.",
        ["ctl.mismatch.default"] = "Los monitores guardados no coinciden con los actuales.",
        ["ctl.overlap.run"] = "Los monitores se superponen en la disposición (puede ocurrir tras girar o cambiar la resolución). Ajusta la disposición y pulsa «Aplicar».",
        ["ctl.need2"] = "La corrección necesita 2 o más monitores.",
        ["ctl.badsize"] = "Tamaño de pantalla no válido (ancho 100–3000 mm, alto 60–3000 mm).",
        ["ctl.overlap.apply"] = "Los monitores se superponen en el escritorio. Colócalos de modo que no se superpongan.",
        ["hook.fail"] = "No se pudo instalar el gancho del ratón.",

        ["core.nosaved"] = "No hay configuración guardada.",
        ["core.count"] = "El número de monitores no coincide con la configuración guardada (guardados {0}, ahora {1}).",
        ["core.nomatch"] = "No se pueden emparejar con seguridad los monitores guardados con los actuales (se cambió un monitor o su puerto).",
    };

    // ================= Deutsch =================
    private static readonly Dictionary<string, string> De = new()
    {
        ["app.title"] = "MonitorBridge — Mauszeiger-Korrektur für mehrere Monitore",
        ["app.error"] = "Ein unerwarteter Fehler ist aufgetreten.\n{0}",
        ["tray.hint"] = "Nach dem Schließen des Fensters läuft die App im Infobereich weiter.",
        ["tray.open"] = "Fenster öffnen",
        ["tray.toggle"] = "Korrektur ein-/ausschalten",
        ["tray.exit"] = "Beenden",
        ["tray.on"] = "Korrektur ausschalten",
        ["tray.off"] = "Korrektur einschalten",
        ["tray.text"] = "MonitorBridge — Korrektur {0}",
        ["state.on"] = "an",
        ["state.suspended"] = "pausiert",
        ["state.off"] = "aus",

        ["sel.title"] = "Ausgewählter Monitor",
        ["sel.source"] = "Größenquelle: {0}",
        ["size.title"] = "Tatsächliche Bildschirmgröße",
        ["size.diag"] = "Diagonale",
        ["size.inch"] = "Zoll",
        ["size.w"] = "Breite",
        ["size.h"] = "Höhe",
        ["pos.title"] = "Position auf dem Schreibtisch (obere linke Ecke)",
        ["pos.x"] = "Horizontal X",
        ["pos.y"] = "Vertikal Y",
        ["pos.hint"] = "Y wächst nach unten. Gleiches Y bei zwei Monitoren richtet die Oberkanten aus; gleiches Y + Höhe richtet die Unterkanten aus.",
        ["align.title"] = "An Nachbarmonitor ausrichten",
        ["align.start"] = "Oben/Links",
        ["align.center"] = "Mitte",
        ["align.end"] = "Unten/Rechts",
        ["help.drag"] = "Beim Ziehen rastet der Monitor an den Kanten anderer Monitore ein (Alt gedrückt halten zum Deaktivieren). Die Windows-Skalierung wird nicht als physische Größe verwendet; konnten die Monitordaten nicht gelesen werden, gilt eine Schätzung von 24 Zoll – bitte gib die tatsächliche Größe ein.",
        ["btn.identify"] = "Bildschirme identifizieren",
        ["btn.redetect"] = "Neu erkennen",
        ["btn.reset"] = "Auf Standard zurücksetzen",
        ["btn.apply"] = "Übernehmen",
        ["btn.on"] = "Einschalten",
        ["btn.off"] = "Ausschalten",
        ["chk.startup"] = "Mit Windows im Infobereich starten",
        ["lang.label"] = "Sprache",

        ["banner.on"] = "● Korrektur an",
        ["banner.on.detail"] = "{0} Monitore · {1} passierbare Kantenabschnitt(e)",
        ["banner.on.dirty"] = "  —  Klicke auf „Übernehmen“, um deine Änderungen zu verwenden.",
        ["banner.susp"] = "▲ Korrektur pausiert (Windows-Standardverhalten)",
        ["banner.susp.detail"] = "Die Anzeigekonfiguration wird erneut geprüft.",
        ["banner.off"] = "○ Korrektur aus (Windows-Standardverhalten)",
        ["banner.off.one"] = "Es wurde nur 1 Monitor erkannt. Schließe 2 oder mehr an, um die Korrektur zu nutzen.",
        ["banner.off.many"] = "Prüfe, ob die grünen Abschnitte zur tatsächlichen Anordnung auf deinem Schreibtisch passen, und klicke dann auf „Übernehmen“, um die Korrektur einzuschalten.",

        ["dlg.apply.fail"] = "Anwenden nicht möglich",
        ["dlg.reset.text"] = "Die gespeicherte Anordnung wird gelöscht und auf die Standardwerte anhand der Windows-Anordnung zurückgesetzt.\nDie Korrektur wird ausgeschaltet; klicke auf „Übernehmen“, um sie wieder einzuschalten.\n(Die Windows-Anzeigeeinstellungen werden nicht geändert.)",
        ["dlg.reset.title"] = "Auf Standard zurücksetzen",
        ["dlg.startup.fail"] = "Die Autostart-Einstellung konnte nicht geändert werden.\n{0}",
        ["dlg.error"] = "Fehler",

        ["canvas.legend"] = "■ Grüne Linie: hier kann der Mauszeiger wechseln (andere Kanten sind gesperrt)",

        ["vm.title"] = "Nr. {0} · {1}",
        ["vm.detail"] = "{0}×{1} · Skalierung {2}% · {3}",
        ["vm.landscape"] = "Querformat",
        ["vm.portrait90"] = "Hochformat (90°)",
        ["vm.landscape180"] = "Querformat (gedreht)",
        ["vm.portrait270"] = "Hochformat (270°)",
        ["vm.src.user"] = "Eingegeben/geschätzt",
        ["vm.src.edid"] = "Monitordaten (EDID)",

        ["ctl.one"] = "Es ist nur ein Monitor vorhanden, daher ist keine Korrektur nötig. Die Korrektur wurde pausiert.",
        ["ctl.mismatch"] = "{0} Aus Sicherheitsgründen wurde die Korrektur ausgeschaltet. Prüfe die Anordnung und klicke auf „Übernehmen“, um sie wieder einzuschalten.",
        ["ctl.mismatch.default"] = "Die gespeicherten Monitore stimmen nicht mit den aktuellen überein.",
        ["ctl.overlap.run"] = "Die Monitore überlappen sich in der Anordnung (das kann nach einer Drehung oder Auflösungsänderung passieren). Passe die Anordnung an und klicke auf „Übernehmen“.",
        ["ctl.need2"] = "Die Korrektur benötigt 2 oder mehr Monitore.",
        ["ctl.badsize"] = "Ungültige Bildschirmgröße (Breite 100–3000 mm, Höhe 60–3000 mm).",
        ["ctl.overlap.apply"] = "Die Monitore überlappen sich auf dem Schreibtisch. Ordne sie so an, dass sie sich nicht überlappen.",
        ["hook.fail"] = "Der Maus-Hook konnte nicht installiert werden.",

        ["core.nosaved"] = "Keine gespeicherten Einstellungen.",
        ["core.count"] = "Die Anzahl der Monitore weicht von den gespeicherten Einstellungen ab (gespeichert {0}, jetzt {1}).",
        ["core.nomatch"] = "Die gespeicherten Monitore lassen sich nicht sicher den aktuellen zuordnen (ein Monitor wurde ersetzt oder sein Anschluss geändert).",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
    {
        ["en"] = En, ["ko"] = Ko, ["zh"] = Zh, ["hi"] = Hi, ["ja"] = Ja, ["es"] = Es, ["de"] = De,
    };
}

/// <summary>XAML에서 {svc:L key} 로 번역 문자열을 쓴다(창을 만들 때 한 번 평가).</summary>
[MarkupExtensionReturnType(typeof(string))]
internal sealed class LExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    public LExtension() { }
    public LExtension(string key) { Key = key; }
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
