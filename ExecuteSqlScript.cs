using System;
using System.IO;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        string connectionString = "Server=tcp:teamsync-prod-sql-2026.database.windows.net,1433;Initial Catalog=TeamSyncDb;Persist Security Info=False;User ID=teamsyncadmin;Password=xpress23@;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        string sqlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TeamSync", "create_file_attachments_table.sql");

        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                Console.WriteLine("✓ Connected to Azure SQL Database");

                string sqlScript = File.ReadAllText(sqlFilePath);

                using (SqlCommand command = new SqlCommand(sqlScript, connection))
                {
                    command.CommandTimeout = 60;
                    command.ExecuteNonQuery();
                    Console.WriteLine("✓ FileAttachments table created successfully!");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
