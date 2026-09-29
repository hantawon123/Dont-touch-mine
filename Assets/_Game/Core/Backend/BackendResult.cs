namespace Game.Core.Backend
{
    /// <summary>
    /// Outcome of a backend call that answers with nothing but success.
    /// </summary>
    /// <remarks>
    /// Returned rather than thrown. A friend list that will not load is an
    /// ordinary thing on a player's network, and making every caller wrap its
    /// calls in try/catch to handle the ordinary case gets exceptions used for
    /// control flow. Programmer mistakes still throw.
    /// </remarks>
    public readonly struct BackendResult
    {
        public readonly bool Ok;

        public readonly BackendFailure Failure;

        private BackendResult(bool ok, BackendFailure failure)
        {
            Ok = ok;
            Failure = failure;
        }

        public static BackendResult Success() =>
            new BackendResult(true, BackendFailure.None);

        public static BackendResult Failed(BackendFailure failure) =>
            new BackendResult(false, failure);
    }

    /// <summary>
    /// Outcome of a backend call that answers with a value.
    /// </summary>
    /// <remarks>
    /// <see cref="Value"/> is only meaningful when <see cref="Ok"/> is true. It
    /// is left at its default on failure rather than being given a stand-in,
    /// because an empty friend list that means "you have no friends" and one
    /// that means "the request failed" have to stay distinguishable.
    /// </remarks>
    public readonly struct BackendResult<T>
    {
        public readonly bool Ok;

        public readonly BackendFailure Failure;

        public readonly T Value;

        private BackendResult(bool ok, BackendFailure failure, T value)
        {
            Ok = ok;
            Failure = failure;
            Value = value;
        }

        public static BackendResult<T> Success(T value) =>
            new BackendResult<T>(true, BackendFailure.None, value);

        public static BackendResult<T> Failed(BackendFailure failure) =>
            new BackendResult<T>(false, failure, default);
    }
}
