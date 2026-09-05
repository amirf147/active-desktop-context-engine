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

internal static class SessionAnalyzer
{
    public static async Task RunDeepAnalysisSpikeAsync(string? customDbPath)
    {
        string dbPath = customDbPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ADCE", "adce_history.db");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("  ADCE Historical Database Telemetry Audit & Statistical Analysis        ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
        Console.WriteLine($"Database: {dbPath}");

        if (!File.Exists(dbPath))
        {
            Console.WriteLine("[ERROR] Database file not found.");
            return;
        }

        var fi = new FileInfo(dbPath);
        Console.WriteLine($"Size    : {fi.Length / (1024.0 * 1024.0):F2} MB ({fi.Length:N0} bytes)");
        Console.WriteLine($"Modified: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n");

        string connStr = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            DefaultTimeout = 5
        }.ToString();

        await using var conn = new SqliteConnection(connStr);
        await conn.OpenAsync();

        // 1. Overall Metrics
        long totalSnapshots = 0;
        string minTime = "", maxTime = "";
        await using (var cmd = new SqliteCommand("SELECT COUNT(*), MIN(timestamp_utc), MAX(timestamp_utc) FROM desktop_snapshots;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (await r.ReadAsync())
            {
                totalSnapshots = r.GetInt64(0);
                minTime = r.IsDBNull(1) ? "" : r.GetString(1);
                maxTime = r.IsDBNull(2) ? "" : r.GetString(2);
            }
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[1] DATASET LIFECYCLE SUMMARY");
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.ResetColor();
        Console.WriteLine($" Total Recorded Snapshots : {totalSnapshots:N0}");
        Console.WriteLine($" Earliest Timestamp       : {minTime}");
        Console.WriteLine($" Latest Timestamp         : {maxTime}");
        if (DateTimeOffset.TryParse(minTime, out var tMin) && DateTimeOffset.TryParse(maxTime, out var tMax))
        {
            var span = tMax - tMin;
            Console.WriteLine($" Total Timespan           : {span.TotalHours:F2} hours ({span.TotalMinutes:F1} minutes)");
            Console.WriteLine($" Ingestion Rate           : {(totalSnapshots / Math.Max(1.0, span.TotalMinutes)):F1} snapshots/minute");
        }

        // 2. Process Breakdown
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[2] PROCESS & APPLICATION BREAKDOWN");
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.ResetColor();
        Console.WriteLine($" {"Process",-25} | {"Count",8} | {"Pct",6} | {"Archetype Breakdown"}");
        Console.WriteLine(new string('-', 74));

        await using (var cmd = new SqliteCommand(@"
            SELECT process_name, COUNT(*) as cnt, archetype
            FROM desktop_snapshots
            GROUP BY process_name, archetype
            ORDER BY cnt DESC;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            var procMap = new Dictionary<string, (int Total, List<string> Archetypes)>();
            while (await r.ReadAsync())
            {
                string proc = r.GetString(0);
                int count = r.GetInt32(1);
                var arch = (DesktopAppArchetype)r.GetInt32(2);
                if (!procMap.ContainsKey(proc))
                {
                    procMap[proc] = (0, new List<string>());
                }
                var cur = procMap[proc];
                cur.Total += count;
                cur.Archetypes.Add($"{arch} ({count})");
                procMap[proc] = cur;
            }

            foreach (var (proc, info) in procMap.OrderByDescending(p => p.Value.Total))
            {
                double pct = (double)info.Total / totalSnapshots * 100.0;
                string archStr = string.Join(", ", info.Archetypes);
                Console.WriteLine($" {proc,-25} | {info.Total,8:N0} | {pct,5:F1}% | {archStr}");
            }
        }

        // 3. Semantic Zone Breakdown
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[3] SEMANTIC ZONE DISTRIBUTION");
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.ResetColor();
        Console.WriteLine($" {"Semantic Zone",-22} | {"Count",8} | {"Pct",6} | Distribution Bar");
        Console.WriteLine(new string('-', 74));

        await using (var cmd = new SqliteCommand(@"
            SELECT focus_semantic_zone, COUNT(*) as cnt
            FROM desktop_snapshots
            GROUP BY focus_semantic_zone
            ORDER BY cnt DESC;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                var zone = (DesktopSemanticZone)r.GetInt32(0);
                int count = r.GetInt32(1);
                double pct = (double)count / totalSnapshots * 100.0;
                int barLen = (int)(pct / 3.0);
                string bar = new string('█', Math.Max(1, barLen));
                Console.WriteLine($" {zone,-22} | {count,8:N0} | {pct,5:F1}% | {bar}");
            }
        }

        // 4. Unknown Controls Telemetry Breakdown
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[4] UNKNOWN (ZONE 0) CLUSTER AUDIT - TOP 35 CANDIDATES FOR SELF-HEALING");
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.ResetColor();
        Console.WriteLine($" {"Count",5} | {"Process",-18} | {"Class",-20} | {"Control",-10} | {"Element / Context Name"}");
        Console.WriteLine(new string('-', 85));

        await using (var cmd = new SqliteCommand(@"
            SELECT COUNT(*) as cnt, process_name, class_name, focus_control_type, focus_element_name, active_file_or_tab
            FROM desktop_snapshots
            WHERE focus_semantic_zone = 0
            GROUP BY process_name, class_name, focus_control_type, focus_element_name
            ORDER BY cnt DESC
            LIMIT 35;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                int count = r.GetInt32(0);
                string proc = r.GetString(1);
                if (proc.Length > 18) proc = proc[..18];
                string cls = r.GetString(2);
                if (cls.Length > 20) cls = cls[..20];
                string cType = r.IsDBNull(3) ? "" : r.GetString(3);
                if (cType.Length > 10) cType = cType[..10];
                string elem = r.IsDBNull(4) ? "" : r.GetString(4);
                if (string.IsNullOrWhiteSpace(elem) && !r.IsDBNull(5)) elem = r.GetString(5);
                if (elem.Length > 30) elem = elem[..27] + "...";

                Console.WriteLine($" {count,5} | {proc,-18} | {cls,-20} | {cType,-10} | '{elem}'");
            }
        }

        // 5. IDE Sub-Panel Granular Audit
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[5] IDE (ANTIGRAVITY / VS CODE) ZONE AUDIT");
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.ResetColor();

        await using (var cmd = new SqliteCommand(@"
            SELECT focus_semantic_zone, focus_control_type, focus_element_name, COUNT(*) as cnt
            FROM desktop_snapshots
            WHERE process_name LIKE '%Antigravity%' OR process_name LIKE '%Code%'
            GROUP BY focus_semantic_zone, focus_control_type, focus_element_name
            ORDER BY cnt DESC
            LIMIT 25;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            Console.WriteLine($" {"Zone",-20} | {"Control",-10} | {"Count",6} | {"Element Name"}");
            Console.WriteLine(new string('-', 70));
            while (await r.ReadAsync())
            {
                var zone = (DesktopSemanticZone)r.GetInt32(0);
                string cType = r.IsDBNull(1) ? "" : r.GetString(1);
                string elem = r.IsDBNull(2) ? "" : r.GetString(2);
                int count = r.GetInt32(3);
                if (elem.Length > 32) elem = elem[..29] + "...";
                Console.WriteLine($" {zone,-20} | {cType,-10} | {count,6} | '{elem}'");
            }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n==========================================================================");
        Console.WriteLine("  DATABASE TELEMETRY AUDIT COMPLETE                                      ");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
    }
}
