namespace Velora.Rendering.Sdl;

internal sealed class ThreadAffinity
{
    private int? _ownerThreadId;

    public bool IsBound => _ownerThreadId.HasValue;

    public void BindCurrent()
    {
        if (_ownerThreadId.HasValue)
        {
            AssertCurrent();
            return;
        }

        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    public void AssertCurrent()
    {
        if (!_ownerThreadId.HasValue)
        {
            throw new InvalidOperationException("The SDL render thread has not been bound.");
        }

        int current = Environment.CurrentManagedThreadId;
        if (current != _ownerThreadId.Value)
        {
            throw new InvalidOperationException(
                $"SDL rendering is thread-affine. Owner thread is {_ownerThreadId.Value}; current thread is {current}.");
        }
    }

    public void Reset()
    {
        if (_ownerThreadId.HasValue)
        {
            AssertCurrent();
        }

        _ownerThreadId = null;
    }
}
