namespace RestoranYonetim.Security;

// İzin değişince Bump() çağrılır; sürüm numarası önbellek anahtarının parçası olduğundan eski girdiler otomatik geçersiz kalır (bkz. PermissionClaimsTransformation).
public sealed class PermissionCacheVersion
{
    private int _version;

    public int Current => Volatile.Read(ref _version);

    public void Bump() => Interlocked.Increment(ref _version);
}
