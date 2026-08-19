-- Cada microsserviço tem o seu próprio database.
-- Nenhum serviço lê ou escreve nas tabelas do outro.
CREATE DATABASE korp_inventory;
CREATE DATABASE korp_billing;
