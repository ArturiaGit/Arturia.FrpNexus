using System;

namespace Arturia.FrpNexus.Desktop.ViewModels;

internal static class ViewModelErrorText
{
    public static string ForUser(string operation, Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return $"{operation}已取消。";
        }

        return $"{operation}失败，请检查输入、网络或本地数据状态后重试。";
    }
}
