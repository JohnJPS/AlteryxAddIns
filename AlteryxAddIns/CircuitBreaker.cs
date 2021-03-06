﻿namespace JDunkerley.AlteryxAddIns
{
    using System.Collections.Generic;

    using Framework;
    using Framework.Attributes;
    using Framework.Factories;
    using Framework.Interfaces;

    /// <summary>
    /// Pass through the Input flow if no data received on Breaker
    /// </summary>
    public class CircuitBreaker
        : BaseTool<CircuitBreaker.Config, CircuitBreaker.Engine>, AlteryxGuiToolkit.Plugins.IPlugin
    {
        /// <summary>
        /// Configuration Class
        /// </summary>
        public class Config
        {
            /// <summary>
            /// Returns a <see cref="System.String" /> that represents this instance.
            /// </summary>
            public override string ToString() => string.Empty;
        }

        /// <summary>
        /// Engine for Circuit Breaker
        /// </summary>
        /// <seealso cref="Framework.BaseEngine{Config}" />
        public class Engine : BaseEngine<Config>
        {
            private Queue<AlteryxRecordInfoNet.Record> _inputRecords;

            private bool _failed;

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
                this.Breaker = inputPropertyFactory.Build(recordCopierFactory, this.ShowDebugMessages);
                this.Breaker.InitCalled += (property, args) => this._failed = false;
                this.Breaker.RecordPushed += (sender, args) =>
                    {
                        if (this._failed)
                        {
                            args.Success = false;
                            return;
                        }

                        this._failed = true;
                        this.ExecutionComplete();
                    };
                this.Breaker.Closed += (sender, args) =>
                    {
                        if (!this._failed)
                        {
                            while ((this._inputRecords?.Count ?? 0) > 0)
                            {
                                var record = this._inputRecords?.Dequeue();
                                this.Output?.Push(record);
                            }
                        }

                        if (this.Input.State == ConnectionState.Closed)
                        {
                            this.Output?.Close(true);
                        }
                    };

                this.Input = inputPropertyFactory.Build(recordCopierFactory, this.ShowDebugMessages);
                this.Input.InitCalled += (property, args) =>
                    {
                        this._inputRecords = new Queue<AlteryxRecordInfoNet.Record>();
                        this.Output?.Init(this.Input.RecordInfo);
                    };
                this.Input.ProgressUpdated += (sender, args) => this.Output?.UpdateProgress(this._failed ? 1.0 : args.Progress, true);
                this.Input.RecordPushed += (sender, args) =>
                    {
                        if (this._failed)
                        {
                            args.Success = false;
                            return;
                        }

                        var record = this.Input.RecordInfo.CreateRecord();
                        this.Input.Copier.Copy(record, args.RecordData);

                        if (this.Breaker.State == ConnectionState.Closed)
                        {
                            this.Output?.Push(record);
                        }
                        else
                        {
                            this._inputRecords.Enqueue(record);
                        }
                    };
                this.Input.Closed += (sender, args) =>
                    {
                        if (this.Breaker.State == ConnectionState.Closed)
                        {
                            this.Output?.Close(true);
                        }
                    };
            }

            [CharLabel('B')]
            [Ordering(1)]
            public IInputProperty Breaker { get; }

            [CharLabel('I')]
            [Ordering(2)]
            public IInputProperty Input { get; }

            [CharLabel('O')]
            public OutputHelper Output { get; set; }
        }
    }
}