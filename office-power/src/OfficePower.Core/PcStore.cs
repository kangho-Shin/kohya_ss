using System.Text.Json;
using System.Security.Cryptography;

namespace OfficePower.Core;

public interface ISecretProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

public sealed class PcStore(string path, ISecretProtector protector)
{
    private sealed record Document(int Version, List<PcRecord> Computers);

    public async Task<List<PcRecord>> LoadAsync()
    {
        if (!File.Exists(path)) return [];
        try
        {
            var document = JsonSerializer.Deserialize<Document>(await File.ReadAllTextAsync(path), JsonFormat.Options);
            if (document is null || document.Version != 1 || document.Computers is null)
                throw new InvalidDataException("등록 파일 버전을 확인하세요.");
            var list = document.Computers.Select(pc => pc with { Token = protector.Unprotect(pc.Token) }).ToList();
            foreach (var pc in list) pc.Validate();
            return list;
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException or ArgumentException)
        {
            throw new InvalidDataException("등록 파일을 읽거나 복호화할 수 없습니다. 파일을 보존한 상태로 확인하세요.", ex);
        }
    }

    public async Task SaveAsync(IEnumerable<PcRecord> records)
    {
        var list = records.ToList();
        foreach (var pc in list) pc.Validate();
        if (list.Select(p => p.Id).Distinct().Count() != list.Count ||
            list.Select(p => p.Ip).Distinct().Count() != list.Count)
            throw new ArgumentException("중복된 IP 또는 PC 항목이 있습니다.");
        var encrypted = list.Select(pc => pc with { Token = protector.Protect(pc.Token) }).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid() + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Document(1, encrypted), JsonFormat.Options));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
