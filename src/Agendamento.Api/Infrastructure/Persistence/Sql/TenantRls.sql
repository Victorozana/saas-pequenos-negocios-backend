-- Applied only after all tenant-owned tables exist. Runtime roles own no tables.
SELECT pg_advisory_xact_lock(728349102);
DO $$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'agendamento_app_user') THEN
    CREATE ROLE agendamento_app_user NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
  END IF;
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'agendamento_system_user') THEN
    CREATE ROLE agendamento_system_user NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
  END IF;
  IF EXISTS (SELECT FROM pg_roles WHERE rolname IN ('agendamento_app_user','agendamento_system_user') AND (rolsuper OR rolbypassrls)) THEN
    RAISE EXCEPTION 'Runtime roles must not bypass RLS';
  END IF;
END $$;
GRANT USAGE ON SCHEMA public TO agendamento_app_user, agendamento_system_user;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM agendamento_app_user;
GRANT SELECT ON tenants, "SaasPlans" TO agendamento_app_user;
GRANT SELECT, INSERT, UPDATE ON "Users", "EmailVerificationTokens" TO agendamento_app_user;
GRANT SELECT, INSERT, UPDATE ON tenants, "Users", "EmailVerificationTokens", "IdempotencyRecords", "SaasPlans" TO agendamento_system_user;
ALTER TABLE "tenant_fiscal_profiles" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "tenant_fiscal_profiles" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "tenant_fiscal_profiles" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "tenant_fiscal_profiles" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "tenant_fiscal_profiles" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "tenant_memberships" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "tenant_memberships" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "tenant_memberships" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "tenant_memberships" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "tenant_memberships" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "Customers" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Customers" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "Customers" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "Customers" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "Customers" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "ServiceItems" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ServiceItems" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "ServiceItems" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "ServiceItems" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "ServiceItems" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "Quotations" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Quotations" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "Quotations" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "Quotations" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "Quotations" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "QuotationItem" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "QuotationItem" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "QuotationItem" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "QuotationItem" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "QuotationItem" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "WorkOrders" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "WorkOrders" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "WorkOrders" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "WorkOrders" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "WorkOrders" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "WorkOrderItems" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "WorkOrderItems" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "WorkOrderItems" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "WorkOrderItems" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "WorkOrderItems" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "Appointments" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Appointments" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "Appointments" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "Appointments" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "Appointments" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "ReceivableTitles" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ReceivableTitles" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "ReceivableTitles" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "ReceivableTitles" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "ReceivableTitles" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "ReceivablePayments" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ReceivablePayments" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "ReceivablePayments" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "ReceivablePayments" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "ReceivablePayments" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "PayableTitles" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PayableTitles" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "PayableTitles" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "PayableTitles" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "PayableTitles" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "PayablePayments" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PayablePayments" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "PayablePayments" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "PayablePayments" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "PayablePayments" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "NotificationMessages" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "NotificationMessages" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "NotificationMessages" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "NotificationMessages" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "NotificationMessages" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "TenantSubscriptions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "TenantSubscriptions" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "TenantSubscriptions" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "TenantSubscriptions" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "TenantSubscriptions" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE "team_invitations" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "team_invitations" FORCE ROW LEVEL SECURITY;
GRANT SELECT, INSERT, UPDATE, DELETE ON "team_invitations" TO agendamento_app_user;
CREATE POLICY tenant_isolation ON "team_invitations" TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_guard ON "team_invitations" AS RESTRICTIVE TO agendamento_app_user
 USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
 WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON "tenant_fiscal_profiles" TO agendamento_system_user;
CREATE POLICY system_operations ON "tenant_fiscal_profiles" TO agendamento_system_user USING (true) WITH CHECK (true);
GRANT SELECT, INSERT, UPDATE ON "tenant_memberships" TO agendamento_system_user;
CREATE POLICY system_operations ON "tenant_memberships" TO agendamento_system_user USING (true) WITH CHECK (true);
GRANT SELECT, INSERT, UPDATE ON "team_invitations" TO agendamento_system_user;
CREATE POLICY system_operations ON "team_invitations" TO agendamento_system_user USING (true) WITH CHECK (true);
GRANT SELECT, INSERT, UPDATE ON "Customers" TO agendamento_system_user;
CREATE POLICY system_operations ON "Customers" TO agendamento_system_user USING (true) WITH CHECK (true);
GRANT SELECT, INSERT, UPDATE ON "ServiceItems" TO agendamento_system_user;
CREATE POLICY system_operations ON "ServiceItems" TO agendamento_system_user USING (true) WITH CHECK (true);
GRANT SELECT, INSERT, UPDATE ON "NotificationMessages" TO agendamento_system_user;
CREATE POLICY system_operations ON "NotificationMessages" TO agendamento_system_user USING (true) WITH CHECK (true);
GRANT SELECT, INSERT, UPDATE ON "TenantSubscriptions" TO agendamento_system_user;
CREATE POLICY system_operations ON "TenantSubscriptions" TO agendamento_system_user USING (true) WITH CHECK (true);

-- A valid tenant must not attach an aggregate/dependent to a different tenant.
-- Derive composite FKs from the physical schema, preserving each original delete action.
DO $$
DECLARE f record; action text; constraint_name text;
BEGIN
 FOR f IN
   SELECT child.relname AS child_table, parent.relname AS parent_table,
          ca.attname AS child_column, pa.attname AS parent_column, fk.confdeltype
   FROM pg_constraint fk
   JOIN pg_class child ON child.oid=fk.conrelid
   JOIN pg_class parent ON parent.oid=fk.confrelid
   JOIN pg_attribute ca ON ca.attrelid=child.oid AND ca.attnum=fk.conkey[1]
   JOIN pg_attribute pa ON pa.attrelid=parent.oid AND pa.attnum=fk.confkey[1]
   WHERE fk.contype='f' AND array_length(fk.conkey,1)=1
     AND child.relnamespace='public'::regnamespace
     AND EXISTS (SELECT FROM pg_attribute WHERE attrelid=child.oid AND attname='tenant_id')
     AND EXISTS (SELECT FROM pg_attribute WHERE attrelid=parent.oid AND attname='tenant_id')
 LOOP
   EXECUTE format('CREATE UNIQUE INDEX IF NOT EXISTS %I ON %I (tenant_id, %I)',
                  'UX_rls_' || f.parent_table || '_' || f.parent_column, f.parent_table, f.parent_column);
   action := CASE f.confdeltype WHEN 'c' THEN 'CASCADE' WHEN 'r' THEN 'RESTRICT'
     WHEN 'n' THEN format('SET NULL (%I)', f.child_column) ELSE 'NO ACTION' END;
   constraint_name := 'FK_rls_' || f.child_table || '_' || f.child_column;
   EXECUTE format('ALTER TABLE %I ADD CONSTRAINT %I FOREIGN KEY (tenant_id, %I) REFERENCES %I (tenant_id, %I) ON DELETE %s',
                  f.child_table, constraint_name, f.child_column, f.parent_table, f.parent_column, action);
 END LOOP;
END $$;
