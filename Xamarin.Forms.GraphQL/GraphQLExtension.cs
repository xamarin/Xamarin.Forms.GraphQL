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

using System;
using System.Collections.Generic;

using Xamarin.Forms.Xaml;

namespace Xamarin.Forms.GraphQL
{
	[ContentProperty(nameof(Field))]
	public sealed class GraphQLExtension : IMarkupExtension<BindingBase>
	{
		public static IDictionary<BindableProperty, BindableProperty> TemplatableProperties = new Dictionary<BindableProperty, BindableProperty> {
			{ItemsView<Cell>.ItemsSourceProperty, ItemsView<Cell>.ItemTemplateProperty}
		};
		public Uri EndPoint { get; set; }
		public string Field { get; set; }
		public string Argument { get; set; }
		public Binding ArgumentBinding { get; set; }
		public string Extra { get; set; }

		BindingBase IMarkupExtension<BindingBase>.ProvideValue(IServiceProvider serviceProvider)
		{
			var targetElement = serviceProvider?.GetService<IProvideValueTarget>()?.TargetObject as BindableObject ?? throw new XamlParseException("Missing serviceProvider, or ProvideValueTarget, or TargetObject", serviceProvider?.GetService<IXmlLineInfoProvider>()?.XmlLineInfo ?? new XmlLineInfo());
			var targetProperty = serviceProvider?.GetService<IProvideValueTarget>()?.TargetProperty as BindableProperty ?? throw new XamlParseException("Missing serviceProvider, or ProvideValueTarget, or TargetObject", serviceProvider?.GetService<IXmlLineInfoProvider>()?.XmlLineInfo ?? new XmlLineInfo());

			var argument = Argument;
			if (GetVariable(argument, ArgumentBinding, targetElement))
				argument = $"{Argument}: ${variableChangedHandler.VariableId}";
			(var gqlField, var bindingPath) = ParseField(Field, argument);

			//When Binding to a bindingcontext, return a GraphQLObject. Same for ListView.ItemSource
			if (targetProperty == BindableObject.BindingContextProperty) {
				var gObject = new GraphQLObject { EndPoint = EndPoint, Field = gqlField };
				if (Extra != null)
					((IGraphQLObject)gObject).AddQueryField(new GraphQLField(Extra));
				if (variableChangedHandler != null)
					((IGraphQLObject)gObject).AddVariable(variableChangedHandler);
				return new Binding(".", mode: BindingMode.OneWay, converter: new GraphQLObjectConverter(), converterParameter: gObject);
			}
			if (TemplatableProperties.TryGetValue(targetProperty, out var itemTemplateProperty)) {
				var gObject = new GraphQLObject { EndPoint = EndPoint, Field = gqlField };
				if (Extra != null)
					((IGraphQLObject)gObject).AddQueryField(new GraphQLField(Extra));
				if (variableChangedHandler != null)
					((IGraphQLObject)gObject).AddVariable(variableChangedHandler);
				ApplyContextToMockTemplatedElement(targetElement, itemTemplateProperty, gObject);
				return new Binding(".", mode: BindingMode.OneWay, converter: new GraphQLObjectConverter(), converterParameter: gObject);
			}
			if (Extra != null)
				gqlField.SubFields = new List<IGraphQLField> { new GraphQLField(Extra) };
			(targetElement.BindingContext as IGraphQLObject)?.AddQueryField(gqlField);
			if (variableChangedHandler != null)
				(targetElement.BindingContext as IGraphQLObject)?.AddVariable(variableChangedHandler);

			targetElement.BindingContextChanged += (o, e) => {
				(((BindableObject)o).BindingContext as IGraphQLObject)?.AddQueryField(gqlField);
				if (variableChangedHandler != null)
					(((BindableObject)o).BindingContext as IGraphQLObject)?.AddVariable(variableChangedHandler);
			};

			return new Binding($"{nameof(IGraphQLObject.Data)}{bindingPath}", BindingMode.OneWay, converter: new GraphQLValueConverter());
		}

		object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => (this as IMarkupExtension<BindingBase>).ProvideValue(serviceProvider);

		static (GraphQLField field, string bindingPath) ParseField(string field, string argument)
		{
			// accepts [alias:]field0[.field1]*
			string fieldname = null;
			string alias = null;

			var parts = field?.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts?.Length > 2) throw new FormatException();
			if (parts?.Length == 1)         //fieldname
				fieldname = parts[0].Trim();
			if (parts?.Length == 2) {       //alias:fieldname
				alias = parts[0].Trim();
				fieldname = parts[1].Trim();
			}

			var path = fieldname?.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
			if (path == null || path.Length == 0)
				return (null, null);

			var gqlField = new GraphQLField(path[0], argument, alias);
			var bindingPath = $"[{alias ?? path[0]}]";
			var lastField = gqlField;
			for (var i = 1; i < path.Length; i++) {
				var f = new GraphQLField(path[i]);
				(lastField.SubFields ?? (lastField.SubFields = new List<IGraphQLField>())).Add(f);
				lastField = f;
				bindingPath += $".[{path[i]}]";
			}
			return (gqlField, bindingPath);
		}

		BindableProperty VariableProperty;
		VariableChangeHandler variableChangedHandler;
		void OnVariablePropertyChanged(BindableObject bindable, object oldValue, object newValue)
		{
			if (variableChangedHandler == null)
				return;
			variableChangedHandler.VariableValue = (string)newValue;
			variableChangedHandler.Notify();
		}

		static int variableCount;
		bool GetVariable(string argument, BindingBase argumentBinding, BindableObject targetElement)
		{
			if (argument == null || argumentBinding == null)
				return false;
			var variableId = $"{argument}{variableCount++}";
			VariableProperty = BindableProperty.Create("Variable", typeof(string), typeof(GraphQLExtension), default(string), propertyChanged: OnVariablePropertyChanged);
			targetElement.SetBinding(VariableProperty, argumentBinding);
			variableChangedHandler = new VariableChangeHandler {
				VariableId = variableId,
				VariableValue = (string)targetElement.GetValue(VariableProperty),
			};
			return true;
		}

		void ApplyContextToMockTemplatedElement(BindableObject targetElement, BindableProperty itemTemplateProperty, GraphQLObject gObject)
		{
			if (targetElement == null)
				throw new ArgumentNullException(nameof(gObject));
			if (itemTemplateProperty == null)
				throw new ArgumentNullException(nameof(itemTemplateProperty));
			if (gObject == null)
				throw new ArgumentNullException(nameof(targetElement));

			var mockElement = (targetElement.GetValue(itemTemplateProperty) as ElementTemplate)?.CreateContent() as BindableObject;
			if (mockElement != null) {
				mockElement.BindingContext = gObject;
				mockElement.BindingContext = null;
				mockElement = null;
			}

			targetElement.PropertyChanged += (o, e) => {
				if (e.PropertyName != itemTemplateProperty.PropertyName)
					return;
				mockElement = (targetElement.GetValue(itemTemplateProperty) as ElementTemplate)?.CreateContent() as BindableObject;
				if (mockElement != null) {
					mockElement.BindingContext = gObject;
					mockElement.BindingContext = null;
					mockElement = null;
				}
			};
		}
	}
}
