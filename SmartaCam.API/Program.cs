using System.Text.Json.Serialization;
using static SmartaCam.AudioRepository;
using static SmartaCam.IAudioRepository;

namespace SmartaCam.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddTransient<IAudioRepository, AudioRepository>();
            builder.Services.AddTransient<ITakeRepository, TakeRepository>();
            builder.Services.AddTransient<IMp3TagSetRepository, Mp3TagSetRepository>();
            builder.Services.AddTransient<ISettingsRepository, SettingsRepository>();
            builder.Services.AddHostedService<DbInitializerHostedService>();
           // builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve);
            //builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);

            builder.Services.AddSwaggerGen(c =>
            {
                c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
                c.IgnoreObsoleteActions();
                c.IgnoreObsoleteProperties();
                c.CustomSchemaIds(type => type.FullName);
            });

            var app = builder.Build();
            app.UseHttpsRedirection();
            if (builder.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

            }
            app.UseRouting();
            app.UseCors(x => x
             .AllowAnyMethod()
             .AllowAnyHeader()
             .SetIsOriginAllowed(origin => true) // allow any origin  
             .AllowCredentials());
        //    app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

           //  Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            UIRepository uIRepository = new();
            _ = Task.Run(async () => { await uIRepository.SessionInitAsync(); });

              app.MapControllers();

            app.Run();
        }
        
        public class DbInitializerHostedService : IHostedService
        {
            public async Task StartAsync(CancellationToken stoppingToken)
            {
				// The code in here will run when the application starts, and block the startup process until finished
				var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SmartaCam");
				if (!Directory.Exists(dbPath))
				{
					DirectoryInfo di = Directory.CreateDirectory(dbPath);
				}
				using (var context = new SmartaCamContext())
                {
                    context.Database.EnsureCreated();
                }

            }

            public Task StopAsync(CancellationToken stoppingToken)
            {
                // The code in here will run when the application stops
                return Task.CompletedTask;
            }
        }
    }
}
