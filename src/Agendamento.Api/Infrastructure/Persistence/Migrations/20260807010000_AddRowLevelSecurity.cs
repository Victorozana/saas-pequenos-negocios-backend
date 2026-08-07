using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    public partial class AddRowLevelSecurity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$ 
BEGIN 
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'agendamento_app_user') THEN 
    CREATE ROLE agendamento_app_user; 
  END IF; 
END $$;

GRANT USAGE ON SCHEMA public TO agendamento_app_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO agendamento_app_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO agendamento_app_user;

ALTER TABLE tenant_fiscal_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_memberships ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_policy_fiscal ON tenant_fiscal_profiles
    AS PERMISSIVE FOR ALL
    TO agendamento_app_user
    USING (""TenantId""::text = current_setting('agendamento.current_tenant_id', true));

CREATE POLICY tenant_isolation_policy_memberships ON tenant_memberships
    AS PERMISSIVE FOR ALL
    TO agendamento_app_user
    USING (""TenantId""::text = current_setting('agendamento.current_tenant_id', true));

-- Force RLS even for table owners if they assume the role (just in case)
ALTER TABLE tenant_fiscal_profiles FORCE ROW LEVEL SECURITY;
ALTER TABLE tenant_memberships FORCE ROW LEVEL SECURITY;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS tenant_isolation_policy_fiscal ON tenant_fiscal_profiles;
DROP POLICY IF EXISTS tenant_isolation_policy_memberships ON tenant_memberships;

ALTER TABLE tenant_fiscal_profiles DISABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_memberships DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
