namespace JDunkerley.AlteryxAddins
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    using AlteryxRecordInfoNet;

    using JDunkerley.AlteryxAddIns.Framework;
    using JDunkerley.AlteryxAddIns.Framework.Attributes;
    using JDunkerley.AlteryxAddIns.Framework.ConfigWindows;

    public class NumberParser :
        BaseTool<NumberParser.Config, NumberParser.Engine>, AlteryxGuiToolkit.Plugins.IPlugin
    {
        public class Config
        {
            public Config()
            {
                this.CultureObject = new Lazy<CultureInfo>(() => CultureTypeConverter.GetCulture(this.Culture));
            }

            /// <summary>
            /// Gets or sets the type of the output.
            /// </summary>
            [Category("Output")]
            [Description("Alteryx Type for the Output Field")]
            [TypeConverter(typeof(FixedListTypeConverter<OutputType>))]
            [FieldList(OutputType.Byte, OutputType.Int16, OutputType.Int32, OutputType.Int64, OutputType.Float, OutputType.Double)]
            public OutputType OutputType { get; set; } = OutputType.Double;

            /// <summary>
            /// Gets or sets the name of the output field.
            /// </summary>
            [Category("Output")]
            [Description("Field Name To Use For Output Field")]
            public string OutputFieldName { get; set; } = "Value";

            /// <summary>
            /// Gets or sets the culture.
            /// </summary>
            [TypeConverter(typeof(CultureTypeConverter))]
            [Category("Format")]
            [Description("The Culture Used To Parse The Text Value")]
            public string Culture { get; set; } = CultureTypeConverter.Current;

            /// <summary>
            /// Gets the culture object.
            /// </summary>
            [System.Xml.Serialization.XmlIgnore]
            [Browsable(false)]
            public Lazy<CultureInfo> CultureObject { get; }

            /// <summary>
            /// Gets or sets the name of the input field.
            /// </summary>
            [Category("Input")]
            [Description("The Field On Input Stream To Parse")]
            [TypeConverter(typeof(InputFieldTypeConverter))]
            [InputPropertyName(nameof(Engine.Input), typeof(Engine), FieldType.E_FT_String, FieldType.E_FT_V_String, FieldType.E_FT_V_WString, FieldType.E_FT_WString)]
            public string InputFieldName { get; set; } = "ValueInput";

            /// <summary>
            /// Gets the Input Field Object
            /// </summary>
            [System.Xml.Serialization.XmlIgnore]
            [Browsable(false)]
            [InputPropertyName(nameof(Engine.Input), typeof(Engine), NameProperty = nameof(InputFieldName))]
            public FieldBase InputField { get; set; }

            /// <summary>
            /// ToString used for annotation
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"{this.InputFieldName} ⇒ {this.OutputFieldName}";
        }

        public class Output
        {
             public double? Value { get; set; }
        }

        public class Engine : PassThroughEngine<Config, Output>
        {
            protected override Dictionary<PropertyInfo, FieldDescription> GetDescriptions(Config config)
            {
                var fieldDescription = config?.OutputType.OutputDescription(config.OutputFieldName, 19);
                fieldDescription.Source = nameof(NumberParser);
                fieldDescription.Description = $"{config?.InputFieldName} parsed as a number";

                var output = base.GetDescriptions(config);
                output[output.Keys.First()] = fieldDescription;
                return output;
            }

            protected override Output CalcFunc(RecordData info, Config config)
            {
                string input = config.InputField.GetAsString(info);
                double value;
                bool result = double.TryParse(input, NumberStyles.Any, config.CultureObject.Value, out value);
                return new Output { Value = result ? (double?)value : null };
            }
        }
    }
}