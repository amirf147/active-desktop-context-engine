// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ADCE.Core.Enums;
using Microsoft.Data.Sqlite;

namespace ADCE.Spikes.Diagnostics;

internal static class TimelineVisualizer
{
    private record DbSnapshotRow(
        long Id,
        string TimestampUtc,
        long TimestampUnixMs,
        long Hwnd,
        string WindowTitle,
        string ProcessName,
        string ClassName,
        int Archetype,
        string FocusControlType,
        string FocusElementName,
        int FocusSemanticZone,
        string ActiveFileOrTab,
        string SnapshotJson
    );

    public static async Task RunDatabaseTimelineSpikeAsync(int limit = 20, string? customDbPath = null)
    {
        string dbPath = customDbPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADCE", "adce_history.db");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE SQLite Time-Series Context History & Transition Timeline           ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
        Console.WriteLine($"Database Path: {dbPath}");

        if (!File.Exists(dbPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[NOTE] Database file does not exist yet.");
            Console.WriteLine("       Launch the ADCE Daemon first to start recording context transitions:");
            Console.WriteLine("       dotnet run --project src/ADCE.Daemon -- --hud\n");
            Console.ResetColor();
            return;
        }

        var fileInfo = new FileInfo(dbPath);
        Console.WriteLine($"Database Size: {fileInfo.Length / 1024.0:F1} KB | Last Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n");

        var rows = new List<DbSnapshotRow>();
        long totalCount = 0;

        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            DefaultTimeout = 3
        }.ToString();

        try
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync();

            // Total count
            await using (var countCmd = new SqliteCommand("SELECT COUNT(*) FROM desktop_snapshots;", conn))
            {
                var countObj = await countCmd.ExecuteScalarAsync();
                totalCount = countObj != null ? Convert.ToInt64(countObj) : 0;
            }

            // Fetch recent limit rows
            const string query = """
                SELECT id, timestamp_utc, timestamp_unix_ms, hwnd, window_title, process_name, class_name, archetype,
                       focus_control_type, focus_element_name, focus_semantic_zone, active_file_or_tab, snapshot_json
                FROM desktop_snapshots
                ORDER BY timestamp_unix_ms DESC, id DESC
                LIMIT @limit;
                """;

            await using (var cmd = new SqliteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@limit", limit);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new DbSnapshotRow(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetInt64(2),
                        reader.GetInt64(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetString(6),
                        reader.GetInt32(7),
                        reader.IsDBNull(8) ? "" : reader.GetString(8),
                        reader.IsDBNull(9) ? "" : reader.GetString(9),
                        reader.GetInt32(10),
                        reader.IsDBNull(11) ? "" : reader.GetString(11),
                        reader.GetString(12)
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Failed to query SQLite database: {ex.Message}");
            Console.ResetColor();
            return;
        }

        if (rows.Count == 0)
        {
            Console.WriteLine("[INFO] Database is currently empty (0 snapshots recorded).");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[QUERY SUCCESS] Displaying {rows.Count} most recent transitions (Total in DB: {totalCount:N0} snapshots)");
        Console.ResetColor();

        // Chronological order for timeline display
        rows.Reverse();

        Console.WriteLine("\n" + new string('=', 110));
        Console.WriteLine($"{"#",-5} | {"TIME (UTC)",-12} | {"PROCESS",-14} | {"SEMANTIC ZONE",-20} | {"ACTIVE CONTEXT / TAB / FILE",-50}");
        Console.WriteLine(new string('-', 110));

        var processDuration = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var zoneCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var unknownElements = new List<(string Process, string Class, string ControlType, string ElementName)>();

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            string timeStr = DateTime.TryParse(r.TimestampUtc, out var dt) ? dt.ToString("HH:mm:ss.fff") : r.TimestampUtc;
            string proc = r.ProcessName.Length > 14 ? r.ProcessName[..14] : r.ProcessName;
            var zone = (DesktopSemanticZone)r.FocusSemanticZone;
            string zoneStr = $"[{zone}]";
            if (zoneStr.Length > 20) zoneStr = zoneStr[..20];

            string target = !string.IsNullOrWhiteSpace(r.ActiveFileOrTab) ? r.ActiveFileOrTab : r.WindowTitle;
            if (target.Length > 50) target = target[..47] + "...";

            if (zone == DesktopSemanticZone.Unknown)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                unknownElements.Add((r.ProcessName, r.ClassName, r.FocusControlType, r.FocusElementName));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.WriteLine($"{r.Id,-5} | {timeStr,-12} | {proc,-14} | {zoneStr,-20} | {target,-50}");
            Console.ResetColor();

            processDuration[r.ProcessName] = processDuration.GetValueOrDefault(r.ProcessName) + 1;
            zoneCount[zone.ToString()] = zoneCount.GetValueOrDefault(zone.ToString()) + 1;
        }

        Console.WriteLine(new string('=', 110));

        // 1. Application Distribution Bar Chart
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n[1] Application Transition Distribution:");
        Console.ResetColor();
        foreach (var (proc, count) in processDuration.OrderByDescending(p => p.Value))
        {
            double pct = (double)count / rows.Count * 100.0;
            int barLen = (int)(pct / 4);
            string bar = new string('█', Math.Max(1, barLen));
            Console.WriteLine($"  {proc,-16} [{bar,-25}] {pct,5:F1}% ({count} transitions)");
        }

        // 2. Semantic Zone Distribution
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n[2] Semantic Zone Distribution:");
        Console.ResetColor();
        foreach (var (zn, count) in zoneCount.OrderByDescending(z => z.Value))
        {
            double pct = (double)count / rows.Count * 100.0;
            int barLen = (int)(pct / 4);
            string bar = new string('▓', Math.Max(1, barLen));
            Console.WriteLine($"  {zn,-20} [{bar,-25}] {pct,5:F1}% ({count} snapshots)");
        }

        // 3. Unknown Telemetry Discovery Section
        if (unknownElements.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[3] Discovery Telemetry: {unknownElements.Count} Unknown Zone Transition(s) Detected");
            Console.WriteLine("    These represent UI controls where semantic zone heuristics can be extended:");
            Console.ResetColor();

            var distinctUnknowns = unknownElements.Distinct().Take(5);
            foreach (var unk in distinctUnknowns)
            {
                Console.WriteLine($"    • App: '{unk.Process}' | Class: '{unk.Class}' | ControlType: '{unk.ControlType}' | Name: '{unk.ElementName}'");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[3] Discovery Telemetry: 100% of analyzed transitions mapped to known semantic zones.");
            Console.ResetColor();
        }

        Console.WriteLine();
    }
}
