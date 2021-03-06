namespace JDunkerley.AlteryxAddIns
{
    using System;
    using System.ComponentModel;

    using AlteryxRecordInfoNet;

    using Framework;
    using Framework.Attributes;
    using Framework.ConfigWindows;
    using Framework.Factories;
    using Framework.Interfaces;

    /// <summary>
    /// Take a value and format as a string
    /// </summary>
    public class StringFormatter :
        BaseTool<StringFormatter.Config, StringFormatter.Engine>, AlteryxGuiToolkit.Plugins.IPlugin
    {
        /// <summary>
        /// Configuration object for the Formatter Tool
        /// </summary>
        public class Config : ConfigWithIncomingConnection
        {
            /// <summary>
            /// Specify the name of the  formatted field in the Output
            /// </summary>
            [Category("Output")]
            [Description("Field Name To Use For Output Field")]
            public string OutputFieldName { get; set; } = "FormattedValue";

            /// <summary>
            /// Specify the length of the Output field
            /// </summary>
            [Category("Output")]
            public int OutputFieldLength { get; set; } = 64;

            /// <summary>
            /// Specify the culture to use for formatting the value
            /// </summary>
            [Category("Format")]
            [TypeConverter(typeof(CultureTypeConverter))]
            [Description("The Culture Used To Format The Value")]
            public string Culture { get; set; } = CultureTypeConverter.Current;

            /// <summary>
            /// Specify the name of the field to format
            /// </summary>
            [Category("Input")]
            [TypeConverter(typeof(InputFieldTypeConverter))]
            [Description("The Field On Input Stream To Format")]
            [InputPropertyName(nameof(Engine.Input), typeof(Engine), FieldType.E_FT_Bool, FieldType.E_FT_Byte, FieldType.E_FT_Int16, FieldType.E_FT_Int32, FieldType.E_FT_Int64, FieldType.E_FT_Float, FieldType.E_FT_Double, FieldType.E_FT_FixedDecimal, FieldType.E_FT_Date, FieldType.E_FT_DateTime, FieldType.E_FT_Time)]
            public string InputFieldName { get; set; } = "Value";

            /// <summary>
            /// Specify the format to be applied
            /// </summary>
            [Category("Format")]
            [Description("The Format String To Use (blank to use default)")]
            public string FormatString { get; set; }

            /// <summary>
            /// ToString used for annotation
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"{this.InputFieldName}=>{this.OutputFieldName} [{this.FormatString}]";

            /// <summary>
            /// Create Formatter <see cref="Func{TResult}"/> Delegate
            /// </summary>
            /// <param name="inputFieldBase"></param>
            /// <returns></returns>
            public Func<RecordData, string> CreateFormatter(FieldBase inputFieldBase)
            {
                var format = this.FormatString;
                var culture = CultureTypeConverter.GetCulture(this.Culture);

                if (string.IsNullOrWhiteSpace(format))
                {
                    return inputFieldBase.GetAsString;
                }

                switch (inputFieldBase.FieldType)
                {
                    case FieldType.E_FT_Bool:
                        return r => inputFieldBase.GetAsBool(r)?.ToString(culture);
                    case FieldType.E_FT_Byte:
                    case FieldType.E_FT_Int16:
                    case FieldType.E_FT_Int32:
                    case FieldType.E_FT_Int64:
                        return r => inputFieldBase.GetAsInt64(r)?.ToString(format, culture);
                    case FieldType.E_FT_Float:
                    case FieldType.E_FT_Double:
                    case FieldType.E_FT_FixedDecimal:
                        return r => inputFieldBase.GetAsDouble(r)?.ToString(format, culture);
                    case FieldType.E_FT_Date:
                    case FieldType.E_FT_DateTime:
                        return r => inputFieldBase.GetAsDateTime(r)?.ToString(format, culture);
                    case FieldType.E_FT_Time:
                        return r => inputFieldBase.GetAsTimeSpan(r)?.ToString(format, culture);
                }

                return null;
            }
        }

        /// <summary>
        /// Formatter Tool Engine
        /// </summary>
        public class Engine : BaseEngine<Config>
        {
            private IRecordCopier _copier;

            private FieldBase _outputFieldBase;

            private Func<RecordData, string> _formatter;

            /// <summary>
            /// Constructor For Alteryx
            /// </summary>
            public Engine()
                : this(new RecordCopierFactory(), new InputPropertyFactory())
            {
            }

            /// <summary>
            /// Create An Engine
            /// </summary>
            /// <param name="recordCopierFactory">Factory to create copiers</param>
            /// <param name="inputPropertyFactory">Factory to create input properties</param>
            internal Engine(IRecordCopierFactory recordCopierFactory, IInputPropertyFactory inputPropertyFactory)
                : base(recordCopierFactory)

            {
                this.Input = inputPropertyFactory.Build(recordCopierFactory, this.ShowDebugMessages);
                this.Input.InitCalled += (sender, args) => args.Success = this.InitFunc(this.Input.RecordInfo);
                this.Input.ProgressUpdated += (sender, args) => this.Output.UpdateProgress(args.Progress, true);
                this.Input.RecordPushed += (sender, args) => this.PushFunc(args.RecordData);
                this.Input.Closed += (sender, args) => this.Output?.Close(true);
            }

            /// <summary>
            /// Gets the input stream.
            /// </summary>
            [CharLabel('I')]
            public IInputProperty Input { get; }

            /// <summary>
            /// Gets or sets the output stream.
            /// </summary>
            [CharLabel('O')]
            public OutputHelper Output { get; set; }

            private bool InitFunc(RecordInfo info)
            {
                // Get Input Field
                var inputFieldBase = info.GetFieldByName(this.ConfigObject.InputFieldName, false);
                if (inputFieldBase == null)
                {
                    return false;
                }

                // Create Output Format
                var fieldDescription = new FieldDescription(this.ConfigObject.OutputFieldName, FieldType.E_FT_V_WString) { Size = this.ConfigObject.OutputFieldLength, Source = nameof(StringFormatter) };
                this.Output?.Init(FieldDescription.CreateRecordInfo(info, fieldDescription));
                this._outputFieldBase = this.Output?[this.ConfigObject.OutputFieldName];

                // Create the Copier
                this._copier = this.RecordCopierFactory.CreateCopier(info, this.Output?.RecordInfo, this.ConfigObject.OutputFieldName);

                // Create the Formatter function
                this._formatter = this.ConfigObject.CreateFormatter(inputFieldBase);

                return this._formatter != null;
            }

            private void PushFunc(RecordData r)
            {
                var record = this.Output.Record;
                record.Reset();

                this._copier.Copy(record, r);

                string result = this._formatter(r);

                if (result != null)
                {
                    this._outputFieldBase.SetFromString(record, result);
                }
                else
                {
                    this._outputFieldBase.SetNull(record);
                }

                this.Output?.Push(record);
            }
        }
    }
}