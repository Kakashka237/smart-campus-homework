using System.Diagnostics;
using OpenTelemetry;

namespace SmartCampus.Canteen.Api.Telemetry
{
    /// <summary>
    /// Единый цифровой след (CALS): связь «задача Jira > коммит > релиз > трасса в проде».
    /// </summary>
    public static class CampusTelemetry
    {
        public const string ServiceName = "SmartCampus.Canteen";
        public const string ServiceVersion = "1.9.0";

        /// <summary>Задача Jira, в рамках которой внесено изменение.</summary>
        public const string JiraIssue = "CAMPUS-104";

        public const string TagJiraIssue = "jira.issue";
        public const string TagTransactionId = "campus.transaction_id";
        public const string TagStudentCardId = "campus.student_card_id";
        public const string TagAmount = "campus.amount";
        public const string TagSanityCheck = "campus.sanity_check";
        public const string TagMigrationPhase = "campus.migration_phase";

        public static readonly ActivitySource Source = new(ServiceName, ServiceVersion);

        /// <summary>
        /// Устанавливает обязательные теги на текущий span (Activity.Current, созданный
        /// инструментированием ASP.NET Core) и на дочерний бизнес-span, а также кладёт
        /// jira.issue в Baggage, чтобы тег пробрасывался в нижестоящие сервисы
        /// через заголовок W3C "baggage".
        /// </summary>
        public static void TagReceipt(Activity? activity, Guid transactionId, string studentCardId)
        {
            if (activity is null)
            {
                return;
            }

            activity.SetTag(TagJiraIssue, JiraIssue);
            activity.SetTag(TagTransactionId, transactionId.ToString());
            activity.SetTag(TagStudentCardId, studentCardId);
            Baggage.SetBaggage(TagJiraIssue, JiraIssue);
        }
    }
}
