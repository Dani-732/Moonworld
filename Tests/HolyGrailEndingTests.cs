using System;
using MoonWorld;

internal static class HolyGrailEndingTests
{
    private static int passed;

    private static void Expect(bool value, string name)
    {
        if (!value) throw new Exception("失败: " + name);
        passed++;
    }

    public static void Main()
    {
        Expect(HolyGrailEndingPolicy.DeadlineFor(1200, 60000) == 61200, "一日倒计时使用绝对 tick");
        Expect(HolyGrailEndingPolicy.DeadlineFor(int.MaxValue - 10, 60) == int.MaxValue, "倒计时上限保护");
        Expect(HolyGrailEndingPolicy.DeadlineDue(false, 60000, 60000), "到期时应结算");
        Expect(!HolyGrailEndingPolicy.DeadlineDue(false, 60001, 60000), "未到期不得结算");
        Expect(!HolyGrailEndingPolicy.DeadlineDue(true, 1, int.MaxValue), "实体化愿望取消消散");
        Expect(!HolyGrailEndingPolicy.DeadlineDue(false, -1, int.MaxValue), "已清除期限不得重复结算");
        Expect(HolyGrailEndingPolicy.ShouldDismiss(true, true, false, false), "仅未实体化的玩家从者消散");
        Expect(!HolyGrailEndingPolicy.ShouldDismiss(true, false, false, false), "敌方从者不受玩家结局清理");
        Expect(!HolyGrailEndingPolicy.ShouldDismiss(true, true, true, false), "永久实体化从者不得消散");
        Expect(!HolyGrailEndingPolicy.ShouldDismiss(true, true, false, true), "已退场从者不得重复清理");
        Console.WriteLine("Holy Grail ending policy tests passed: " + passed);
    }
}
