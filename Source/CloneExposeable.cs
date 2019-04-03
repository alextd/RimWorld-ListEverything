using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace List_Everything
{
	public static class CloneExposeable
	{
		//copy of ScribeLoader.InitLoading but from a string instead of a file
		//StringReader instead of StreamReader
		public static void InitLoadingString(this ScribeLoader loader, string data)
		{
			if (Scribe.mode != LoadSaveMode.Inactive)
			{
				Verse.Log.Error("Called InitLoading() but current mode is " + Scribe.mode);
				Scribe.ForceStop();
			}
			if (loader.curParent != null)
			{
				Verse.Log.Error("Current parent is not null in InitLoading");
				loader.curParent = null;
			}
			if (loader.curPathRelToParent != null)
			{
				Verse.Log.Error("Current path relative to parent is not null in InitLoading");
				loader.curPathRelToParent = null;
			}
			try
			{
				using (StringReader streamReader = new StringReader(data))
				{
					using (XmlTextReader xmlTextReader = new XmlTextReader(streamReader))
					{
						XmlDocument xmlDocument = new XmlDocument();
						xmlDocument.Load(xmlTextReader);
						loader.curXmlParent = xmlDocument.DocumentElement;
					}
				}
				Scribe.mode = LoadSaveMode.LoadingVars;
			}
			catch (Exception ex)
			{
				Verse.Log.Error(string.Concat(new object[]
				{
					"Exception while init loading data: ",
					data,
					"\n",
					ex
				}));
				loader.ForceStop();
				throw;
			}
		}

		public static T LoadFromString<T>(string data) where T:IExposable
		{
			T saveable = default(T);
			try
			{
				//Here's the new call, otherwise this is just like other places that use Scribe.loader.InitLoading
				Scribe.loader.InitLoadingString(data);
				try
				{
					Scribe_Deep.Look<T>(ref saveable, "saveable");  //"saveable" because DebugOutputFor calls it that
					Scribe.loader.FinalizeLoading();
				}
				catch
				{
					Scribe.ForceStop();
					throw;
				}
			}
			catch (Exception ex)
			{
				Verse.Log.Error($"Exception loading object({saveable.GetType()}:{saveable}): {ex}");
				Scribe.ForceStop();
			}
			return saveable;
		}

		//Saving string is done in DebugOutputFor already.
		public static string SaveToString(this IExposable saveable)
		{
			//XML tags so Scribe_Deep has an outer xml to look inside
			return "<xml>" + Scribe.saver.DebugOutputFor(saveable) + "</xml>";
		}

		//Simple copy object using Scribe system
		public static T Clone<T>(this T from) where T : IExposable
		{
			string data = from.SaveToString();
			return LoadFromString<T>(data);
		}

		public static List<T> CloneAll<T>(this List<T> list) where T:IExposable
		{
			List < T > res = new List<T>(list.Count);
			foreach (T obj in list)
				res.Add(obj.Clone());
			return res;
		}
	}
}