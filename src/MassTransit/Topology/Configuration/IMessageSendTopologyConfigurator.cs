// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Topology.Configuration
{
    using System;


    /// <summary>
    /// Configures the sending of a message type, allowing filters to be applied
    /// on send.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public interface IMessageSendTopologyConfigurator<TMessage> :
        IMessageSendTopologyConfigurator,
        IMessageSendTopology<TMessage>
        where TMessage : class
    {
        void Add(IMessageSendTopology<TMessage> sendTopology);

        /// <summary>
        /// Adds a delegated configuration to the send topology, which is called before any topologies
        /// in this configuration.
        /// </summary>
        /// <param name="configuration"></param>
        void AddDelegate(IMessageSendTopology<TMessage> configuration);

        /// <summary>
        /// Adds a convention to the message send topology configuration, which can be modified
        /// </summary>
        /// <param name="convention"></param>
        void AddConvention(IMessageSendTopologyConvention<TMessage> convention);

        /// <summary>
        /// Update a convention if available, otherwise, throw an exception
        /// </summary>
        /// <typeparam name="TConvention"></typeparam>
        /// <param name="update">Called if the convention already exists</param>
        /// <returns></returns>
        void UpdateConvention<TConvention>(Func<TConvention, TConvention> update)
            where TConvention : class, IMessageSendTopologyConvention<TMessage>;

        /// <summary>
        /// Returns the first convention that matches the interface type specified, to allow it to be customized
        /// and or replaced.
        /// </summary>
        /// <typeparam name="TConvention"></typeparam>
        /// <param name="add">Called if the convention does not already exist</param>
        /// <param name="update">Called if the convention already exists</param>
        /// <returns></returns>
        void AddOrUpdateConvention<TConvention>(Func<TConvention> add, Func<TConvention, TConvention> update)
            where TConvention : class, IMessageSendTopologyConvention<TMessage>;
    }


    public interface IMessageSendTopologyConfigurator
    {
    }
}