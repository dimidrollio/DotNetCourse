using DotNetCourse.Data;
using DotNetCourse.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Tree;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using DotNetCourse.Utility;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using Azure.Core.GeoJson;

namespace DotNetCourse.Controllers
{
	public class HomeController : Controller
	{
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly ApplicationDbContext _db;
		public HomeController(ApplicationDbContext db, IWebHostEnvironment webHostEnvironment)
		{
			_db = db;
			_webHostEnvironment = webHostEnvironment;
		}


		public IActionResult Index(int? id, string? jsonString, string? textString)
		{
			var fileToGet = _db.Files.FirstOrDefault(file => file.Id == id);
			if (id == 0 || fileToGet == null)
			{
				return View();
			}
			/*if(fileToGet.Type == SD.Json_Type)
			{
				
			}
			else if (fileToGet.Type == SD.Text_Type)
			{

				var res = GetTxtToPrintable((string)textString, (int)id);
				return View(res);
			}*/
			var res = GetJsonToPrintableResult((string)jsonString, (int)id);
			return View(res);
		}

		public IActionResult Create()
		{
			return View();
		}

		[HttpPost]
		public IActionResult Create(IFormFile? file)
		{
			if (file == null || !(file.ContentType == SD.Json_Type /*|| file.ContentType == SD.Text_Type*/))
			{
				return BadRequest("Enter valid file (.json)");
			}
			var content = FileToString(file);

			InputFile fileToAdd = new()
			{
				Name = file.Name,
				Type = file.ContentType,
				Content = content
			};

			_db.Files.Add(fileToAdd);
			_db.SaveChanges();

			return RedirectToAction(nameof(Index), new { id = fileToAdd.Id, jsonString = content });
		}

		public string FileToString(IFormFile file)
		{
			var stringBuilder = new StringBuilder();    //read json to string 
			using (var reader = new StreamReader(file.OpenReadStream()))
			{
				while (reader.Peek() >= 0)
					stringBuilder.AppendLine(reader.ReadLine());
			}
			return stringBuilder.ToString();  //string 
		}

		#region Main Task Logic Methods		

		public IEnumerable<string> GetJsonToPrintableResult(string json, int fileId)
		{
			/*  Get Nodes of exact file 
			    If Nodes exist, get a list of them.
				If Nodes do not exist, create and add them to database 
				then get a list of them	*/

			IEnumerable<FileNode> nodesToCheck = _db.Nodes.Where(node => node.InputFileId == fileId).OrderBy(_ => _.DisplayOrder).ToList();

			if (nodesToCheck.Any())
			{

				var minDepth = nodesToCheck.Min(n => n.Depth);
				var rootNodes = nodesToCheck.Where(n => n.Depth == minDepth).ToList();

				List<string> StringsToPrint = [];

				foreach (var rootNode in rootNodes)
				{
					GetPrintableNode(rootNode, nodesToCheck, StringsToPrint);
				}
				return StringsToPrint;
			}
			else
			{

				dynamic myObj = JsonConvert.DeserializeObject(json);

				List<FileNode> allNodes = []; //list of all nodes to add
				AddNodeToList(fileId, myObj, 0, "", allNodes, 0);

				_db.Nodes.AddRange(allNodes); // add them
				_db.SaveChanges();

				nodesToCheck = _db.Nodes.Where(node => node.InputFileId == fileId).OrderBy(_ => _.DisplayOrder).ToList();

				var minDepth = nodesToCheck.Min(n => n.Depth);
				var rootNodes = nodesToCheck.Where(n => n.Depth == minDepth).ToList();


				List<string> stringsToPrint = [];

				foreach (var rootNode in rootNodes)
				{
					GetPrintableNode(rootNode, nodesToCheck, stringsToPrint);
				}
				return stringsToPrint;
			}
		}

		private void GetPrintableNode(FileNode node, IEnumerable<FileNode> allNodes, List<string> ResultList)
		/* add objects we want to print to a ResultList in a recursive way
		 */
		{
			var spacer = string.Join("", Enumerable.Range(0, node.Depth).Select(_ => "    "));
			var nodeChildren = allNodes.Where(n => n.ParentId == node.Id).ToList();

			ResultList.Add(($"{spacer}{node.Key} --> {(node.Value is not null ? node.Value : "")}"));

			if (nodeChildren.Count != 0)
			{

				foreach (var childNode in nodeChildren)
				{
					GetPrintableNode(childNode, allNodes, ResultList);
				}
			}
		}

		private void AddNodeToList(int fileId, JToken token, int depth, string parentId, List<FileNode> nodesToAdd, int displayOrder)
		{
			/*logic of creating nodes and adding them to a list of TreeNode List 
			 * to add them to db in one query 
			 */
			if (token is JProperty)
			{
				var jProp = (JProperty)token;

				FileNode treeNode = new()
				{
					Id = Guid.NewGuid().ToString(),
					DisplayOrder = displayOrder + 1,
					Depth = depth,
					Key = jProp.Name,
					InputFileId = fileId
				};
				var val = jProp.Value;

				string id = treeNode.Id;

				if (!string.IsNullOrEmpty(parentId))
				{
					treeNode.ParentId = parentId;
				}


				if (val is JArray && !val.Values().Children().Any())  // make array with only valusw look more attractive
				{
					var temp = "";
					foreach (var value in val.Values())
					{
						temp += value + " ";
					}
					treeNode.Value = temp;

					nodesToAdd.Add(treeNode);
				}
				else
				{//do we print value
					treeNode.Key = jProp.Name;

					if (val is JValue)
					{
						treeNode.Value = (string)val;
					}
					nodesToAdd.Add(treeNode);
				}


				if (val is JArray) //array children print
				{
					if (val.Values().Children().Any())
					{
						foreach (var child in val.Children())
						{
							displayOrder++;
							AddNodeToList(fileId, child, depth + 1, id, nodesToAdd, displayOrder);
						}
					}
				}

				foreach (var child in jProp.Children()) //all prop children print
				{
					AddNodeToList(fileId, child, depth, id, nodesToAdd, displayOrder + 1);
				}
			}

			else if (token is JObject) //if object is not property, print its` children
			{
				foreach (var child in ((JObject)token).Children())
				{
					displayOrder++;
					AddNodeToList(fileId, child, depth + 1, parentId, nodesToAdd, displayOrder);
				}
			}

		}
		#endregion

		#region Txt Bonus Logic Methods

		public IEnumerable<string> GetTxtToPrintable(string txt, int fileId)
		{
			IEnumerable<FileNode> nodesToCheck = _db.Nodes.Where(node => node.InputFileId == fileId).OrderBy(_ => _.DisplayOrder).ToList();
			if (nodesToCheck.Any())
			{
				var minDepth = nodesToCheck.Min(n => n.Depth);
				var rootNodes = nodesToCheck.Where(n => n.Depth == minDepth).ToList();

				List<string> StringsToPrint = [];

				foreach (var rootNode in rootNodes)
				{
					GetPrintableNode(rootNode, nodesToCheck, StringsToPrint);
				}
				return StringsToPrint;
			}
			else
			{
				List<string> lines = txt.Split("\n", StringSplitOptions.RemoveEmptyEntries).ToList();
				List<FileNode> result = [];

				AddNodeFromLine(fileId, lines, 0, "", 0, result, 0);

				_db.Nodes.AddRange(result);
				_db.SaveChanges();

				nodesToCheck = _db.Nodes.Where(node => node.InputFileId == fileId).OrderBy(_ => _.DisplayOrder).ToList();

				var minDepth = nodesToCheck.Min(n => n.Depth);
				var rootNodes = nodesToCheck.Where(n => n.Depth == minDepth).ToList();


				List<string> stringsToPrint = [];

				foreach (var rootNode in rootNodes)
				{
					GetPrintableNode(rootNode, nodesToCheck, stringsToPrint);
				}
				return stringsToPrint;
			}
		}
		private void AddNodeFromLine(int fileId, List<string> lines, int lineId, string parentId, int depth, List<FileNode> nodesToAdd, int displayOrder)
		{
			var line = lines[lineId];
			List<string> obj = line.Split(":", StringSplitOptions.RemoveEmptyEntries).ToList();
			var strDepth = line.Count(_ => _ == ':');
			string id = Guid.NewGuid().ToString();

			string key = obj[depth];


			FileNode node = new()
			{
				Id = id,
				Depth = depth,
				InputFileId = fileId,
				Key = key,
				DisplayOrder = displayOrder
			};

			if (depth >= 1) // if has parent 
			{
				node.ParentId = nodesToAdd.First(node => node.Key == obj[depth - 1]).Id;
			}

			if (depth + 1 == strDepth)   //if before last - set value 
			{
				node.Value = obj[depth + 1];
				nodesToAdd.Add(node);
				if (lines.Count > lineId+1) //if not last line
				{
					AddNodeFromLine(fileId, lines, lineId + 1, "", 0, nodesToAdd, displayOrder + 1);
				}
			}
			nodesToAdd.Add(node);
			if (depth + 1 < strDepth)
			{
				AddNodeFromLine(fileId, lines, lineId, "", depth + 1, nodesToAdd, displayOrder + 1);
			}
		}
	}

	#endregion
}

