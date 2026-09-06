function onLoad()
{
    var db = new Sql();

    if (!db.open("test.db"))
    {
        print("ERROR: no se pudo abrir la base de datos");
        return;
    }

    print("=== SQLite CRUD Test ===");

    // CREATE TABLE
    var q = new Query("CREATE TABLE IF NOT EXISTS usuarios (id INTEGER PRIMARY KEY AUTOINCREMENT, nombre TEXT, edad INTEGER, email TEXT)");
    if (db.query(q))
        print("[OK] Tabla 'usuarios' creada");
    else
        print("[FAIL] CREATE TABLE: " + db.lastError);

    // INSERT - crear registros
    q = new Query("INSERT INTO usuarios (nombre,edad,email) VALUES ({0},{1},{2})", "Juan", 25, "juan@mail.com");
    if (db.query(q))
        print("[OK] INSERT Juan");
    else
        print("[FAIL] INSERT: " + db.lastError);

    q = new Query("INSERT INTO usuarios (nombre,edad,email) VALUES ({0},{1},{2})", "Maria", 30, "maria@mail.com");
    if (db.query(q))
        print("[OK] INSERT Maria");
    else
        print("[FAIL] INSERT: " + db.lastError);

    q = new Query("INSERT INTO usuarios (nombre,edad,email) VALUES ({0},{1},{2})", "Carlos", 22, "carlos@mail.com");
    if (db.query(q))
        print("[OK] INSERT Carlos");
    else
        print("[FAIL] INSERT: " + db.lastError);

    // READ - leer todos
    print("\n--- SELECT ALL ---");
    q = new Query("SELECT * FROM usuarios");
    if (db.query(q))
    {
        while (db.read)
        {
            print("ID: " + db.value("id") + " | Nombre: " + db.value("nombre") + " | Edad: " + db.value("edad") + " | Email: " + db.value("email"));
        }
    }
    else
        print("[FAIL] SELECT: " + db.lastError);

    // READ - buscar uno
    print("\n--- SELECT WHERE nombre=Maria ---");
    q = new Query("SELECT * FROM usuarios WHERE nombre={0}", "Maria");
    if (db.query(q))
    {
        if (db.read)
            print("Encontrado: " + db.value("nombre") + " - " + db.value("email"));
        else
            print("No encontrado");
    }
    else
        print("[FAIL] SELECT WHERE: " + db.lastError);

    // UPDATE
    print("\n--- UPDATE Carlos edad 22->28 ---");
    q = new Query("UPDATE usuarios SET edad={0} WHERE nombre={1}", 28, "Carlos");
    if (db.query(q))
        print("[OK] UPDATE ejecutado");
    else
        print("[FAIL] UPDATE: " + db.lastError);

    // Verificar UPDATE
    q = new Query("SELECT * FROM usuarios WHERE nombre={0}", "Carlos");
    if (db.query(q))
    {
        if (db.read)
            print("Verificacion: " + db.value("nombre") + " ahora tiene " + db.value("edad") + " anios");
    }

    // DELETE
    print("\n--- DELETE Juan ---");
    q = new Query("DELETE FROM usuarios WHERE nombre={0}", "Juan");
    if (db.query(q))
        print("[OK] DELETE ejecutado");
    else
        print("[FAIL] DELETE: " + db.lastError);

    // Verificar DELETE - contar registros
    q = new Query("SELECT COUNT(*) as total FROM usuarios");
    if (db.query(q))
    {
        if (db.read)
            print("Registros restantes: " + db.value("total"));
    }

    // Limpiar tabla
    q = new Query("DELETE FROM usuarios");
    if (db.query(q))
        print("\n[OK] Tabla limpiada");

    db.close();
    print("\n=== Test completado ===");
}
