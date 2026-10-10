using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PharmacySystem.Models.Common;

namespace PharmacySystem.Extensions;

public static class TempDataExtensions
{
    private const string AlertKey = "_PharmacyAlertMessages";

    public static void SetSuccess(this ITempDataDictionary tempData, string message, string? title = null)
    {
        AddAlert(tempData, new AlertMessage
        {
            Type = AlertType.Success,
            Message = message,
            Title = title
        });
    }

    public static void SetError(this ITempDataDictionary tempData, string message, string? title = null)
    {
        AddAlert(tempData, new AlertMessage
        {
            Type = AlertType.Danger,
            Message = message,
            Title = title
        });
    }

    public static void SetWarning(this ITempDataDictionary tempData, string message, string? title = null)
    {
        AddAlert(tempData, new AlertMessage
        {
            Type = AlertType.Warning,
            Message = message,
            Title = title
        });
    }

    public static void SetInfo(this ITempDataDictionary tempData, string message, string? title = null)
    {
        AddAlert(tempData, new AlertMessage
        {
            Type = AlertType.Info,
            Message = message,
            Title = title
        });
    }

    private static void AddAlert(ITempDataDictionary tempData, AlertMessage alert)
    {
        var alerts = GetAlerts(tempData);
        alerts.Add(alert);
        tempData[AlertKey] = JsonSerializer.Serialize(alerts);
    }

    public static List<AlertMessage> GetAlerts(this ITempDataDictionary tempData)
    {
        if (tempData.TryGetValue(AlertKey, out var value) && value is string json)
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<AlertMessage>>(json);
                return list ?? new List<AlertMessage>();
            }
            catch
            {
                return new List<AlertMessage>();
            }
        }

        return new List<AlertMessage>();
    }
}
