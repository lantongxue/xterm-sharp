using System.Text;
using XtermSharp.Internal.Utilities.Disposables;

namespace XtermSharp.Unicode;

internal sealed class UnicodeRegistry : ITerminalUnicode
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IUnicodeProvider> _providers = new(StringComparer.Ordinal);
    private IUnicodeProvider _active;

    public UnicodeRegistry(string activeVersion)
    {
        RegisterCore(new UnicodeV6Provider());
        RegisterCore(new UnicodeV11Provider());
        RegisterCore(new DotNetGraphemeProvider());
        if (!_providers.TryGetValue(activeVersion, out _active!))
        {
            throw new ArgumentException($"Unknown Unicode provider '{activeVersion}'.", nameof(activeVersion));
        }
    }

    public string ActiveVersion
    {
        get => Volatile.Read(ref _active).Version;
        set
        {
            IUnicodeProvider provider;
            lock (_gate)
            {
                if (!_providers.TryGetValue(value, out provider!))
                {
                    throw new ArgumentException($"Unknown Unicode provider '{value}'.", nameof(value));
                }
                Volatile.Write(ref _active, provider);
            }
            ActiveVersionChanged?.Invoke(provider.Version);
        }
    }

    public event Action<string>? ActiveVersionChanged;

    public IReadOnlyCollection<string> Versions
    {
        get
        {
            lock (_gate)
            {
                return _providers.Keys.Order(StringComparer.Ordinal).ToArray();
            }
        }
    }

    internal IUnicodeProvider ActiveProvider => Volatile.Read(ref _active);

    internal int GetWidth(Rune rune) => ActiveProvider.GetWidth(rune);

    internal int GetStringCellWidth(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        IUnicodeProvider provider = ActiveProvider;
        int result = 0;
        Rune? preceding = null;
        foreach (Rune rune in value.EnumerateRunes())
        {
            UnicodeCharacterProperties properties = provider.GetProperties(rune, preceding);
            result += properties.Width;
            preceding = rune;
        }
        return result;
    }

    public IDisposable Register(IUnicodeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_gate)
        {
            if (!_providers.TryAdd(provider.Version, provider))
            {
                throw new ArgumentException($"Unicode provider '{provider.Version}' is already registered.", nameof(provider));
            }
        }

        return new DelegateDisposable(() =>
        {
            bool activeProviderRemoved = false;
            lock (_gate)
            {
                if (ReferenceEquals(_active, provider))
                {
                    Volatile.Write(ref _active, _providers[UnicodeV6Provider.VersionName]);
                    activeProviderRemoved = true;
                }
                _providers.Remove(provider.Version);
            }
            if (activeProviderRemoved)
            {
                ActiveVersionChanged?.Invoke(UnicodeV6Provider.VersionName);
            }
        });
    }

    private void RegisterCore(IUnicodeProvider provider) => _providers.Add(provider.Version, provider);
}
