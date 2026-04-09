namespace Yumalog.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Tracks BeginScope values for the current asynchronous execution flow.
    /// </summary>
    internal sealed class CorporateScopeProvider
    {
        private readonly AsyncLocal<Scope> _currentScope = new AsyncLocal<Scope>();

        /// <summary>
        /// Pushes a new scope value onto the current execution flow.
        /// </summary>
        /// <param name="state">Scope state supplied by Microsoft logging callers.</param>
        /// <returns>A disposable handle that removes the scope when disposed.</returns>
        public IDisposable Push(object state)
        {
            var parent = _currentScope.Value;
            var scope = new Scope(this, state, parent);
            _currentScope.Value = scope;
            return scope;
        }

        /// <summary>
        /// Captures the currently active scopes in outer-to-inner order.
        /// </summary>
        /// <returns>A materialized list of active scope states.</returns>
        public IReadOnlyList<object> CaptureScopes()
        {
            var scopes = new List<object>();
            var current = _currentScope.Value;

            while (current != null)
            {
                scopes.Add(current.State);
                current = current.Parent;
            }

            scopes.Reverse();
            return scopes;
        }

        private void Pop(Scope scope)
        {
            if (_currentScope.Value == scope)
            {
                _currentScope.Value = scope.Parent;
            }
        }

        private sealed class Scope : IDisposable
        {
            private readonly CorporateScopeProvider _provider;
            private bool _disposed;

            public Scope(CorporateScopeProvider provider, object state, Scope parent)
            {
                _provider = provider;
                State = state;
                Parent = parent;
            }

            public Scope Parent { get; }

            public object State { get; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _provider.Pop(this);
                _disposed = true;
            }
        }
    }
}