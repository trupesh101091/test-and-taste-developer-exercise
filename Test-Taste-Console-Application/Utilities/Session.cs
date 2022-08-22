using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Test_Taste_Console_Application.Domain.DataTransferObjects;
using Test_Taste_Console_Application.Domain.Objects;

namespace Test_Taste_Console_Application.Utilities
{
    public class Session
    {
        /// <summary>
        /// AsyncLocalclass allow asynchronous code to use a kind of async-compatible almost-equivalent of thread local storage.
        /// This means that AsyncLocal<T>can persist a Tvalue across an asynchronous flow. 
        /// Each time you enter an async method a fresh new async context is initiated deriving from its parent async context.
        /// </summary>
        private static AsyncLocal<IEnumerable<Planet>> _Data = new AsyncLocal<IEnumerable<Planet>>();

        public static IEnumerable<Planet> Data
        {
            set
            {
                _Data.Value = value;
            }
            get
            {
                return _Data.Value;
            }
        }
    }
}
