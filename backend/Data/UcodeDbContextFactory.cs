using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ucode.Backend.Data;

public class UcodeDbContextFactory : IDesignTimeDbContextFactory<UcodeDbContext>
{
    public UcodeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UcodeDbContext>();
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                   ?? "Host=localhost;Port=5432;Database=ucode;Username=ucode;Password=ucode";
        optionsBuilder.UseNpgsql(conn);
        return new UcodeDbContext(optionsBuilder.Options);
    }
}
