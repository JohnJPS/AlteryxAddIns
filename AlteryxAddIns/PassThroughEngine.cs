namespace JDunkerley.AlteryxAddins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using AlteryxRecordInfoNet;

    using JDunkerley.AlteryxAddIns.Framework;
    using JDunkerley.AlteryxAddIns.Framework.Attributes;

    public abstract class PassThroughEngine<TConfig, TOutput> : BaseEngine<TConfig>
        where TConfig: new()
    {
        private RecordCopier _copier;

        private RecordInfo _outputRecordInfo;

        private TConfig _config;

        private Dictionary<PropertyInfo, FieldDescription> _fieldDescriptions;

        public PassThroughEngine()
        {
            this.Input = new InputProperty(
                initFunc: info => this.InitFunc(info, nameof(this.Input)),
                progressAction: d => this.Output.UpdateProgress(d),
                pushFunc: this.PushFunc,
                closedAction: () =>
                    {
                        this._fieldDescriptions = null;
                        this._config = default(TConfig);
                        this.Output?.Close();
                    });
        }

        /// <summary>
        /// Gets the input stream.
        /// </summary>
        [CharLabel('I')]
        public InputProperty Input { get; }

        /// <summary>
        /// Gets or sets the output stream.
        /// </summary>
        [CharLabel('O')]
        public PluginOutputConnectionHelper Output { get; set; }

        private bool InitFunc(RecordInfo info, string fieldName)
        {
            this._config = this.GetConfigObject();

            // Set the FieldBase
            if (!this.SetFieldBases(info, fieldName))
            {
                return false;
            }

            this._fieldDescriptions = this.GetDescriptions(this._config);

            var newRecordInfo = Utilities.CreateRecordInfo(info, this._fieldDescriptions.Values.ToArray());
            this._outputRecordInfo = newRecordInfo;
            this.Output?.Init(newRecordInfo, nameof(this.Output), null, this.XmlConfig);

            // Create the Copier
            this._copier = Utilities.CreateCopier(info, newRecordInfo, this._fieldDescriptions.Values.Select(d => d.Name).ToArray());

            return true;
        }

        private bool SetFieldBases(RecordInfo info, string fieldName)
        {
            foreach (var propertyInfo in typeof(TConfig).GetProperties().Where(p => p.PropertyType == typeof(FieldBase)))
            {
                var attrib = propertyInfo.GetAttrib<InputPropertyNameAttribute>();
                if (attrib == null || attrib.FieldName != fieldName || attrib.EngineType != this.GetType())
                {
                    continue;
                }

                string propName = propertyInfo.Name;
                if (!string.IsNullOrWhiteSpace(attrib.NameProperty))
                {
                    propName =
                        typeof(TConfig).GetProperty(attrib.NameProperty).GetGetMethod().Invoke(this._config, null) as string;
                }
                if (propName == null)
                {
                    return false;
                }

                var fieldIndex = info.GetFieldNum(propName, false);
                if (fieldIndex == -1)
                {
                    return false;
                }

                propertyInfo.GetSetMethod().Invoke(this._config, new object[] { info[fieldIndex] });
            }

            return true;
        }

        private bool PushFunc(RecordData r)
        {
            var record = this._outputRecordInfo.CreateRecord();
            this._copier.Copy(record, r);

            var result = this.CalcFunc(r, this._config);

            foreach (var fieldDescription in this._fieldDescriptions)
            {
                var val = fieldDescription.Key.GetGetMethod().Invoke(result, null);
                var field = this._outputRecordInfo.GetFieldByName(fieldDescription.Value.Name, false);
                record.SetValue(field, val);
            }

            this.Output?.PushRecord(record.GetRecord());
            return true;
        }

        protected virtual Dictionary<PropertyInfo, FieldDescription> GetDescriptions(TConfig config)
        {
            var output = new Dictionary<PropertyInfo, FieldDescription>();
            foreach (var propertyInfo in typeof(TOutput).GetProperties())
            {
                OutputType? outputType = Utilities.GetOutputType(propertyInfo.PropertyType);
                if (outputType == null)
                {
                    continue;
                }

                output.Add(propertyInfo, outputType.Value.OutputDescription(propertyInfo.Name, 4096));
            }
            return output;
        }

        protected abstract TOutput CalcFunc(RecordData info, TConfig config);
    }
}