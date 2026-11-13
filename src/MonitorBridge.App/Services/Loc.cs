using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using MonitorBridge.Core;

namespace MonitorBridge.App.Services;

/// <summary>UI 문자열 번역(ko/en). 키가 없으면 영어, 그것도 없으면 키를 그대로 보여준다.</summary>
internal static class Loc
{
    public static string Lang { get; private set; } = "en";
    public static bool English => Lang == "en";

    public static string DefaultFromSystem() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ko", StringComparison.OrdinalIgnoreCase) ? "ko" : "en";

    public static void Set(string? lang)
    {
        Lang = lang == "ko" ? "ko" : "en";
        CoreText.English = Lang == "en";
    }

    public static string T(string key, params object[] args)
    {
        var table = Lang == "ko" ? Ko : En;
        if (!table.TryGetValue(key, out var s) && !En.TryGetValue(key, out s)) s = key;
        return args.Length == 0 ? s : string.Format(CultureInfo.CurrentCulture, s, args);
    }

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

        ["first.title"] = "MonitorBridge — Language",
        ["first.text"] = "Choose the language for the interface.\nYou can change it later in the main window.",
    };

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

        ["first.title"] = "MonitorBridge — 언어",
        ["first.text"] = "인터페이스 언어를 선택하세요.\n나중에 메인 창에서 바꿀 수 있습니다.",
    };
}

/// <summary>XAML에서 {ui:L key} 로 번역 문자열을 쓴다(창을 만들 때 한 번 평가).</summary>
[MarkupExtensionReturnType(typeof(string))]
internal sealed class LExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    public LExtension() { }
    public LExtension(string key) { Key = key; }
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
