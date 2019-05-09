﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony;
using UnityEngine;
using static KButtonMenu;
using static ReportManager;

namespace ExportDailyReports
{
    [HarmonyPatch(typeof(OptionsMenuScreen), "OnPrefabInit")]
    internal class ExportDailyReports_OptionsMenuScreen_OnPrefabInit
	{		
		private static FieldInfo buttonsF = AccessTools.Field(typeof(KButtonMenu), "buttons");

        private static void Postfix(PauseScreen __instance)
        {
            Debug.Log(" === ExportDailyReports_OptionsMenuScreen_OnPrefabInit Postfix === ");           

			var buttonList = new List<ButtonInfo>((IList<ButtonInfo>)buttonsF.GetValue(__instance)) {
				new ButtonInfo("Export Daily Reports", Action.NumActions, OnExportDailyReports)
			};
			buttonsF.SetValue(__instance, buttonList);
		}


        private static void OnExportDailyReports()
        {
			bool error = false;
			List<DailyReport> dailyReports = ReportManager.Instance.reports;

			ReportData data = new ReportData();

			foreach (var report in dailyReports)
            {
                try
                {
					//Debug.Log(report.day);

					AddValueCycle("Cycle", report.day, data.dataGeneral, data.headersGeneral);
					AddValueCycle("Cycle", report.day, data.dataPower, data.headersPower);
					

					int num = 1;
                    foreach (KeyValuePair<ReportManager.ReportType, ReportManager.ReportGroup> reportGroup in ReportManager.Instance.ReportGroups)
                    {
						ReportManager.ReportEntry entry = report.GetEntry(reportGroup.Key);
                        int num2 = num;
                        ReportManager.ReportGroup value = reportGroup.Value;
                        if (num2 != value.group)
                        {
                            ReportManager.ReportGroup value2 = reportGroup.Value;
                            num = value2.group;
                        }
                        int num3;
                        if (entry.accumulate == 0f)
                        {
                            ReportManager.ReportGroup value3 = reportGroup.Value;
                            num3 = (value3.reportIfZero ? 1 : 0);
                        }
                        else
                        {
                            num3 = 1;
                        }
                        bool flag2 = (byte)num3 != 0;
                        ReportManager.ReportGroup value4 = reportGroup.Value;
                        if (value4.isHeader)
                        {
                            //CreateHeader(reportGroup.Value);
                        }
                        else if (flag2)
                        {
							CreateOrUpdateLine(entry, reportGroup.Value, flag2, report.day, data);
						}
                    }

				}
                catch (Exception e)
                {
					error = true;
					Debug.LogError(e);
					InfoDialogScreen infoDialogScreen = (InfoDialogScreen)GameScreenManager.Instance.StartScreen(ScreenPrefabs.Instance.InfoDialogScreen.gameObject, GameScreenManager.Instance.ssOverlayCanvas.gameObject);
					infoDialogScreen.SetHeader("Export Daily Reports").AddPlainText("Error exporting daily reports:\n" + e.ToString());
					infoDialogScreen.Show();
				}				
            }

			if (!error)
			{
				data.headersPower.Sort((f1, f2) =>
					{
						if (f1.Equals("Cycle")) return -1;
						return f1.CompareTo(f2);
					}
				);

				WriteData(dailyReports.Count, data);
			}


		}

		private static void WriteData(int day, ReportData data)
		{
			string reportsPath = System.Reflection.Assembly.GetAssembly(typeof(ExportDailyReports_OptionsMenuScreen_OnPrefabInit)).Location;
			string reportsFile = Path.Combine(Path.GetDirectoryName(SaveLoader.GetActiveSaveFilePath()), Path.GetFileNameWithoutExtension(SaveLoader.GetActiveSaveFilePath()) + "_" + day + "_General" + ".csv");
			string reportsPowerFile = Path.Combine(Path.GetDirectoryName(SaveLoader.GetActiveSaveFilePath()), Path.GetFileNameWithoutExtension(SaveLoader.GetActiveSaveFilePath())  + "_" + day + "_Power" + ".csv");

			try
			{
				WriteCSV(reportsFile, data.dataGeneral, data.headersGeneral);

				WriteCSV(reportsPowerFile, data.dataPower, data.headersPower);

				InfoDialogScreen infoDialogScreen = (InfoDialogScreen)GameScreenManager.Instance.StartScreen(ScreenPrefabs.Instance.InfoDialogScreen.gameObject, GameScreenManager.Instance.ssOverlayCanvas.gameObject);
				infoDialogScreen.SetHeader("Export Daily Reports").AddPlainText("Daily reports exported.");
				infoDialogScreen.Show();
			}
			catch (Exception e)
			{
				Debug.LogError(e);
				InfoDialogScreen infoDialogScreen = (InfoDialogScreen)GameScreenManager.Instance.StartScreen(ScreenPrefabs.Instance.InfoDialogScreen.gameObject, GameScreenManager.Instance.ssOverlayCanvas.gameObject);
				infoDialogScreen.SetHeader("Export Daily Reports").AddPlainText("Error exporting daily reports:\n" + e.ToString());
				infoDialogScreen.Show();
			}
		}

		private static void WriteCSV(string file, Dictionary<int, Dictionary<string, string>> data, List<string> dataH)
		{
			Debug.Log("Exporting Daily Reports: " + file);
			StringBuilder dataS = new StringBuilder();
			foreach (var item in dataH)
			{
				dataS.Append(item + ",");
			}
			dataS.Append("\n");


			foreach (var item in data)
			{
				foreach (var col in dataH)
				{
					if (item.Value.ContainsKey(col))
						dataS.Append(item.Value.GetValueSafe(col) + ",");
					else
						dataS.Append("0.00,");
				}
				dataS.Append("\n");
			}
			File.WriteAllText(file, dataS.ToString());
		}

        private static void CreateHeader(ReportManager.ReportGroup reportGroup)
        {
            Debug.Log("header: "+reportGroup.stringKey);            
        }

        private static void CreateOrUpdateLine(ReportManager.ReportEntry entry, ReportManager.ReportGroup reportGroup, bool is_line_active, int day, ReportData data)
        {
			addedValue = float.NegativeInfinity;
			removedValue = float.NegativeInfinity;
			netValue = float.NegativeInfinity;

			SetLine(entry, reportGroup, day, data);
		}


		private static float addedValue = float.NegativeInfinity;
		private static float removedValue = float.NegativeInfinity;
		private static float netValue = float.NegativeInfinity;
		

        public static void SetLine(ReportManager.ReportEntry entry, ReportManager.ReportGroup reportGroup, int day, ReportData data)
        {
			//Debug.Log("cycle: "+day);

			List<ReportManager.ReportEntry.Note> pos_notes = new List<ReportManager.ReportEntry.Note>();
			entry.IterateNotes(delegate (ReportManager.ReportEntry.Note note)
			{
				if (note.value > 0f)
				{
					pos_notes.Add(note);
					//Debug.Log("pos "+note.note+" = "+note.value);
				}
			});

			List<ReportManager.ReportEntry.Note> neg_notes = new List<ReportManager.ReportEntry.Note>();
            entry.IterateNotes(delegate (ReportManager.ReportEntry.Note note)
            {
                if (note.value < 0f)
                {
                    neg_notes.Add(note);
                    //Debug.Log("neg " + note.note + " = " + note.value);
                }
            });			

			string columnName = (entry.context == null ? reportGroup.stringKey : entry.context);

			if (addedValue != entry.Positive)
            {
				addedValue = entry.Positive;
			}
			if (removedValue != entry.Negative)
            {
				removedValue = entry.Negative;
			}
            if (netValue != entry.Net)
            {                  
				netValue = entry.Net;
			}
            pos_notes.Clear();
            neg_notes.Clear();

			string cleanedColName = CleanHeader(columnName);			

			AddValue(cleanedColName + " (+)", addedValue, data.dataGeneral.GetValueSafe(day), data.headersGeneral);
			AddValue(cleanedColName + " (-)", removedValue, data.dataGeneral.GetValueSafe(day), data.headersGeneral);
			AddValue(cleanedColName + " (=)", netValue, data.dataGeneral.GetValueSafe(day), data.headersGeneral);

			if (cleanedColName.Contains("Power Usage"))
			{
				//Debug.Log(cleanedColName + " TOOLTIP POS:");
				OnNoteTooltip(entry, reportGroup, data.dataPower.GetValueSafe(day), data.headersPower, (ReportManager.ReportEntry.Note note) => IsPositiveNote(note));

				//Debug.Log(cleanedColName + " TOOLTIP NEG:");
				OnNoteTooltip(entry, reportGroup, data.dataPower.GetValueSafe(day), data.headersPower, (ReportManager.ReportEntry.Note note) => IsNegativeNote(note));				
			}

		}


		private static void OnNoteTooltip(ReportManager.ReportEntry entry, ReportManager.ReportGroup reportGroup, Dictionary<string, string> dataP, List<string> dataPH, Func<ReportManager.ReportEntry.Note, bool> is_note_applicable_cb)
		{
			//Debug.Log("OnNoteTooltip");
			List <ReportManager.ReportEntry.Note> notes = new List<ReportManager.ReportEntry.Note>();
			notes.Clear();
			entry.IterateNotes(delegate (ReportManager.ReportEntry.Note note)
			{
				if (is_note_applicable_cb(note))
				{
					notes.Add(note);
				}
			});
		
			foreach (ReportManager.ReportEntry.Note item in Sort(notes, reportGroup.posNoteOrder))
			{
				ReportManager.ReportEntry.Note current = item;				

				string colName = CleanHeader(current.note);

				if (current.value > 0f)
				{				
					AddValue(colName + " (+)", current.value, dataP, dataPH);					
				}
				else
				{
					AddValue(colName + " (-)", current.value, dataP, dataPH);					
				}				
			}
		}

		private static bool IsPositiveNote(ReportManager.ReportEntry.Note note)
		{
			if (note.value > 0f)
			{
				return true;
			}
			return false;
		}

		private static bool IsNegativeNote(ReportManager.ReportEntry.Note note)
		{
			if (note.value < 0f)
			{
				return true;
			}
			return false;
		}

		private static  List<ReportManager.ReportEntry.Note> Sort(List<ReportManager.ReportEntry.Note> notes, ReportManager.ReportEntry.Order order)
		{
			switch (order)
			{
				case ReportManager.ReportEntry.Order.Ascending:
					notes.Sort((ReportManager.ReportEntry.Note x, ReportManager.ReportEntry.Note y) => x.value.CompareTo(y.value));
					break;
				case ReportManager.ReportEntry.Order.Descending:
					notes.Sort((ReportManager.ReportEntry.Note x, ReportManager.ReportEntry.Note y) => y.value.CompareTo(x.value));
					break;
			}
			return notes;
		}


		private static void AddValueCycle(string name, int value, Dictionary<int, Dictionary<string, string>> data, List<string> dataH)
		{
			if (!dataH.Contains("Cycle"))
				dataH.Add("Cycle");
			if (!data.ContainsKey(value))
				data.Add(value, new Dictionary<string, string>());
			if (!data.GetValueSafe(value).ContainsKey(name))
			{
				data.GetValueSafe(value).Add(name, value.ToString());
			}
		}

		private static void AddValue(string name, float value, Dictionary<string, string> data, List<string> dataH)
		{
			if (!dataH.Contains(name))
				dataH.Add(name);
			if (!data.ContainsKey(name))
				data.Add(name, value.ToString("F2"));
		}		
		
		public static string CleanHeader(string txt)
		{
			//string outT = txt.Replace(":", "");
			string outT = txt.Split(':')[0];
			int idx = outT.IndexOf("\">");
			if (idx > 0)
				outT = outT.Substring(idx + 2, outT.IndexOf("</link>") - idx - 2) + outT.Substring(outT.IndexOf("</link>")+7);

			return outT.Trim();
		}
    }
}
