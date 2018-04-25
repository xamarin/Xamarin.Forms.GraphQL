// MIT License
//   
// Copyright(c) 2018 Microsoft
//   
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Xamarin.Forms.GraphQL
{
	public static class GraphQLExtensions
	{
		const int Ident = 2;
		public static string Serialize(this IGraphQLField field, string operationType = "query", string operationName = null, IEnumerable<IGraphQLVariable> variables = null)
		{
			var vardeclaration = variables == null ? null : " (" + string.Join(" ", variables.Select(s => $"${s.VariableId}: {s.VariableType}")) + ")";
			var sb = new StringBuilder($"{operationType} {operationName} {vardeclaration}{{");
			field.SerializeInto(sb, Ident);
			sb.AppendLine("}");
			return sb.ToString();
		}

		static void SerializeInto(this IGraphQLField field, StringBuilder sb, int identation)
		{
			sb.AppendLine();

			sb.Append(new string(' ', identation));
			if (field.Name == null) {
				if (!field.SubFields?.Any() ?? true)
					return;

				foreach (var f in field.SubFields)
					f.SerializeInto(sb, identation + Ident);
				sb.AppendLine();
				return;
			}

			if (field.Alias != null)
				sb.Append($"{field.Alias}: ");
			sb.Append(field.Name);
			if (field.Argument != null)
				sb.Append($" ({field.Argument})");

			if (!field.SubFields?.Any() ?? true)
				return;

			sb.Append(" {");
			foreach (var f in field.SubFields)
				f.SerializeInto(sb, identation + Ident);
			sb.AppendLine();
			sb.Append(new string(' ', identation));
			sb.AppendLine("}");
		}

		internal static IEnumerable<string> GetBindingPath(this IGraphQLField field)
		{
			List<string> path = null;
			while (field != null) {
				if (field.Name != null)
					(path ?? (path = new List<string>())).Add(field.Alias ?? field.Name);
				field = field.SubFields?.SingleOrDefault();
			}
			return path;
		}

		internal static IGraphQLField GetLastField(this IGraphQLField field)
		{
			IGraphQLField prev = null;
			while (field != null) {
				prev = field;
				field = field.SubFields?.SingleOrDefault();
			}
			return prev;
		}

		public static string Serialize(this IEnumerable<IGraphQLVariable> variables)
		{
			if (variables == null)
				return null;

			var sb = new StringBuilder("{");
			sb.AppendLine();
			foreach (var v in variables)
				sb.AppendLine($"\"{v.VariableId}\": \"{v.VariableValue}\"");
			sb.Append("}");
			return sb.ToString();
		}
	}
}