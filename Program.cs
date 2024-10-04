using BPMPlus.Data;
using BPMPlus.Service;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using static BPMPlus.Service.AesAndTimestampService;

namespace BPMPlus
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddScoped<FormReview>();
            builder.Services.AddScoped<AesAndTimestampService>();
            builder.Services.AddScoped<ResetPasswordService>();


            // �qappsettings.jsonŪ���n�J�O�ɳ]�w
            //double LoginExpireMinute = builder.Configuration.GetValue<double>("LoginExpireMinute");

            // �إ����Ҥ����n��A��
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(option =>
            {
                // �ثe�O���ɶ��]�w��60���� (appsettings.json)
                option.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                // �ҥ�cookie�ưʹL��
                option.SlidingExpiration = true;
                option.LoginPath = "/Login/Index";
            });

            // �]�w MVC �P CSRF ����
            builder.Services.AddControllersWithViews(options => {
                // CSRF��w�����A�o�̴N�[�J�������ҽd��Filter���ܡA�ݷ|Controller�N�����A�[�W[AutoValidateAntiforgeryToken]�ݩ�
                //options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            });
            //��JSON�^�Ǥ���r���O�ýX
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);
            });

            //// �ϥΰO����@�� Session �s�x
            //// �L���ɶ�, cookie�z�Lhttps�s�u
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(60);
                options.Cookie.IsEssential = true;
                options.Cookie.HttpOnly = true; 
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            // �ϥ�session
            app.UseSession();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");


            app.Run();
        }
    }
}