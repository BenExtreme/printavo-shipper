using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class MergeStaffLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StaffMembers",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Merge the separate Add Task assignee and delivery-notification
            // lists into one staff list keyed by Printavo user id: assignees
            // first (in their order) with the notify email of the contact
            // linked to the same user, then linked contacts that weren't
            // assignees. Contacts with no Printavo user link can't be carried
            // over (staff are Printavo users now) — re-tick them in Settings.
            migrationBuilder.Sql("""
                WITH
                s AS (SELECT * FROM AppSettings WHERE Id = 1),
                a AS (
                    SELECT json_extract(j.value, '$.Id') AS id, json_extract(j.value, '$.Name') AS name, CAST(j.key AS INTEGER) AS ord
                    FROM s, json_each(CASE WHEN json_valid(s.TaskAssignees) THEN s.TaskAssignees ELSE '[]' END) AS j),
                c AS (
                    SELECT json_extract(j.value, '$.PrintavoUserId') AS id, json_extract(j.value, '$.Name') AS name,
                           json_extract(j.value, '$.Email') AS email, CAST(j.key AS INTEGER) AS ord
                    FROM s, json_each(CASE WHEN json_valid(s.DeliveryNotifyContacts) THEN s.DeliveryNotifyContacts ELSE '[]' END) AS j
                    WHERE json_extract(j.value, '$.PrintavoUserId') IS NOT NULL),
                staff AS (
                    SELECT a.id, a.name, (SELECT c.email FROM c WHERE c.id = a.id LIMIT 1) AS email, a.ord AS ord FROM a
                    UNION ALL
                    SELECT c.id, c.name, c.email, 100000 + c.ord FROM c WHERE c.id NOT IN (SELECT id FROM a))
                UPDATE AppSettings
                SET StaffMembers = (
                    SELECT json_group_array(json_object('PrintavoUserId', id, 'Name', name, 'NotifyEmail', email))
                    FROM (SELECT * FROM staff ORDER BY ord))
                WHERE Id = 1;
                """);

            migrationBuilder.DropColumn(
                name: "DeliveryNotifyContacts",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "TaskAssignees",
                table: "AppSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaskAssignees",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNotifyContacts",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropColumn(
                name: "StaffMembers",
                table: "AppSettings");
        }
    }
}
