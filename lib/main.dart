import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:intl/intl.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sqflite/sqflite.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('ar');
  runApp(const ProfitTrackerApp());
}

class ProfitTrackerApp extends StatelessWidget {
  const ProfitTrackerApp({super.key});

  @override
  Widget build(BuildContext context) {
    final scheme = ColorScheme.fromSeed(
      seedColor: const Color(0xFF0E9F6E),
      brightness: Brightness.light,
    );

    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'Maen Accountings',
      locale: const Locale('ar'),
      supportedLocales: const [Locale('ar')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: scheme,
        scaffoldBackgroundColor: const Color(0xFFF4F7FB),
        appBarTheme: AppBarTheme(
          centerTitle: true,
          backgroundColor: scheme.surface,
          foregroundColor: scheme.onSurface,
          elevation: 0,
          scrolledUnderElevation: 0,
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(16),
            borderSide: BorderSide(color: Colors.grey.shade300),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(16),
            borderSide: BorderSide(color: Colors.grey.shade300),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(16),
            borderSide: BorderSide(color: scheme.primary, width: 1.2),
          ),
        ),
        cardTheme: CardThemeData(
          color: Colors.white,
          elevation: 0,
          surfaceTintColor: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(24),
            side: BorderSide(color: Colors.grey.shade200),
          ),
        ),
        navigationBarTheme: NavigationBarThemeData(
          indicatorColor: scheme.primary.withValues(alpha: 0.12),
          height: 74,
          labelTextStyle: WidgetStateProperty.resolveWith((states) {
            final selected = states.contains(WidgetState.selected);
            return TextStyle(
              fontSize: 12.5,
              fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
            );
          }),
        ),
        snackBarTheme: SnackBarThemeData(
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        ),
      ),
      home: const AppStartupGate(),
    );
  }
}

class ProfitEntry {
  final int? id;
  final DateTime date;
  final double sales;
  final double cost;
  final double expenses;
  final String notes;

  const ProfitEntry({
    this.id,
    required this.date,
    required this.sales,
    required this.cost,
    required this.expenses,
    required this.notes,
  });

  double get grossProfit => sales - cost;
  double get netProfit => grossProfit - expenses;

  Map<String, Object?> toMap() {
    return {
      'id': id,
      'entry_date': DateFormat('yyyy-MM-dd').format(date),
      'sales': sales,
      'cost': cost,
      'expenses': expenses,
      'notes': notes,
    };
  }

  Map<String, Object?> toBackupMap() {
    return {
      'entry_date': DateFormat('yyyy-MM-dd').format(date),
      'sales': sales,
      'cost': cost,
      'expenses': expenses,
      'notes': notes,
    };
  }

  factory ProfitEntry.fromMap(Map<String, Object?> map) {
    return ProfitEntry(
      id: map['id'] as int?,
      date: DateTime.parse(map['entry_date'] as String),
      sales: (map['sales'] as num).toDouble(),
      cost: (map['cost'] as num).toDouble(),
      expenses: (map['expenses'] as num).toDouble(),
      notes: (map['notes'] as String?) ?? '',
    );
  }

  factory ProfitEntry.fromBackupMap(Map<String, dynamic> map) {
    return ProfitEntry(
      date: DateTime.parse(map['entry_date'] as String),
      sales: (map['sales'] as num).toDouble(),
      cost: (map['cost'] as num).toDouble(),
      expenses: (map['expenses'] as num).toDouble(),
      notes: (map['notes'] as String?) ?? '',
    );
  }
}

class DatabaseHelper {
  DatabaseHelper._();

  static final DatabaseHelper instance = DatabaseHelper._();
  static Database? _database;

  Future<Database> get database async {
    if (_database != null) return _database!;
    _database = await _initDatabase();
    return _database!;
  }

  Future<Database> _initDatabase() async {
    final databasesPath = await getDatabasesPath();
    final path = p.join(databasesPath, 'profit_tracker.db');

    return openDatabase(
      path,
      version: 1,
      onCreate: (db, version) async {
        await db.execute('''
          CREATE TABLE entries (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            entry_date TEXT NOT NULL UNIQUE,
            sales REAL NOT NULL,
            cost REAL NOT NULL,
            expenses REAL NOT NULL,
            notes TEXT NOT NULL DEFAULT ''
          )
        ''');
      },
    );
  }

  Future<List<ProfitEntry>> getAllEntries() async {
    final db = await database;
    final maps = await db.query('entries', orderBy: 'entry_date DESC');
    return maps.map(ProfitEntry.fromMap).toList();
  }

  Future<int> countEntries() async {
    final db = await database;
    final result = await db.rawQuery('SELECT COUNT(*) AS total FROM entries');
    return Sqflite.firstIntValue(result) ?? 0;
  }

  Future<void> insertOrReplaceEntry(ProfitEntry entry) async {
    final db = await database;
    await db.insert(
      'entries',
      entry.toMap(),
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<void> replaceAllEntries(List<ProfitEntry> entries) async {
    final db = await database;
    await db.transaction((txn) async {
      await txn.delete('entries');
      for (final entry in entries) {
        await txn.insert(
          'entries',
          entry.toMap()..remove('id'),
          conflictAlgorithm: ConflictAlgorithm.replace,
        );
      }
    });
  }

  Future<void> deleteEntry(int id) async {
    final db = await database;
    await db.delete('entries', where: 'id = ?', whereArgs: [id]);
  }
}

class UserProfile {
  final String email;

  const UserProfile({required this.email});
}

class ProfileStore {
  static const _emailKey = 'user_email';

  Future<UserProfile?> load() async {
    final prefs = await SharedPreferences.getInstance();
    final email = prefs.getString(_emailKey)?.trim().toLowerCase() ?? '';
    if (email.isEmpty) return null;
    return UserProfile(email: email);
  }

  Future<void> saveEmail(String email) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_emailKey, email.trim().toLowerCase());
  }

  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_emailKey);
  }
}

class FirebaseBootstrap {
  FirebaseBootstrap._();

  static bool _initialized = false;
  static bool _available = false;
  static String? _lastError;

  static bool get isAvailable => _available;
  static String? get lastError => _lastError;

  static Future<void> ensureInitialized() async {
    if (_initialized) return;
    _initialized = true;
    try {
      await Firebase.initializeApp();
      _available = true;
      _lastError = null;
    } catch (e) {
      _available = false;
      _lastError = e.toString();
    }
  }
}

class AuthResult {
  final bool success;
  final String message;
  final String? email;
  final bool isNewAccount;

  const AuthResult({
    required this.success,
    required this.message,
    this.email,
    this.isNewAccount = false,
  });
}

class AuthService {
  AuthService._();

  static final AuthService instance = AuthService._();

  bool get isAvailable => FirebaseBootstrap.isAvailable;
  String? get lastError => FirebaseBootstrap.lastError;
  bool get isSignedIn => FirebaseAuth.instance.currentUser != null;
  String? get currentUserEmail => FirebaseAuth.instance.currentUser?.email;

  Future<void> init() async {
    await FirebaseBootstrap.ensureInitialized();
  }

  Future<AuthResult> signIn({
    required String email,
    required String password,
  }) async {
    await init();
    if (!isAvailable) {
      return const AuthResult(
        success: false,
        message: 'Firebase غير مفعّل بعد. أكمل الإعداد أولًا.',
      );
    }

    try {
      final credential = await FirebaseAuth.instance.signInWithEmailAndPassword(
        email: email.trim(),
        password: password,
      );
      return AuthResult(
        success: true,
        message: 'تم تسجيل الدخول بنجاح.',
        email: credential.user?.email ?? email.trim(),
      );
    } on FirebaseAuthException catch (e) {
      return AuthResult(success: false, message: _mapError(e));
    } catch (e) {
      return AuthResult(success: false, message: 'حدث خطأ غير متوقع: $e');
    }
  }

  Future<AuthResult> register({
    required String email,
    required String password,
  }) async {
    await init();
    if (!isAvailable) {
      return const AuthResult(
        success: false,
        message: 'Firebase غير مفعّل بعد. أكمل الإعداد أولًا.',
      );
    }

    try {
      final credential = await FirebaseAuth.instance.createUserWithEmailAndPassword(
        email: email.trim(),
        password: password,
      );
      return AuthResult(
        success: true,
        message: 'تم إنشاء الحساب وتسجيل الدخول بنجاح.',
        email: credential.user?.email ?? email.trim(),
        isNewAccount: true,
      );
    } on FirebaseAuthException catch (e) {
      return AuthResult(success: false, message: _mapError(e));
    } catch (e) {
      return AuthResult(success: false, message: 'حدث خطأ غير متوقع: $e');
    }
  }

  Future<void> signOut() async {
    await init();
    if (!isAvailable) return;
    await FirebaseAuth.instance.signOut();
  }

  String _mapError(FirebaseAuthException e) {
    switch (e.code) {
      case 'invalid-email':
        return 'صيغة الإيميل غير صحيحة.';
      case 'invalid-credential':
        return 'بيانات الدخول غير صحيحة.';
      case 'user-disabled':
        return 'هذا الحساب موقوف.';
      case 'user-not-found':
        return 'لا يوجد حساب بهذا الإيميل.';
      case 'wrong-password':
        return 'كلمة المرور غير صحيحة.';
      case 'email-already-in-use':
        return 'هذا الإيميل مستخدم مسبقًا.';
      case 'weak-password':
        return 'كلمة المرور ضعيفة. استخدم 6 أحرف على الأقل.';
      case 'network-request-failed':
        return 'تعذر الاتصال بالإنترنت. تحقق من الشبكة.';
      case 'too-many-requests':
        return 'تمت محاولات كثيرة. انتظر قليلًا ثم حاول مرة أخرى.';
      default:
        return e.message ?? 'حدث خطأ في المصادقة.';
    }
  }
}

class BackupSnapshotInfo {
  final bool exists;
  final String? email;
  final DateTime? updatedAt;
  final int entriesCount;
  final String? filePath;

  const BackupSnapshotInfo({
    required this.exists,
    this.email,
    this.updatedAt,
    this.entriesCount = 0,
    this.filePath,
  });
}

class BackupService {
  BackupService._();

  static final BackupService instance = BackupService._();
  String? _lastError;

  String? get lastError => _lastError;

  String _normalizeEmail(String email) => email.trim().toLowerCase();

  String _safeFileSegment(String email) {
    return _normalizeEmail(email).replaceAll(RegExp(r'[^a-z0-9]+'), '_');
  }

  Future<File> _backupFile(String email) async {
    final dir = await getApplicationDocumentsDirectory();
    return File(p.join(dir.path, 'profit_tracker_${_safeFileSegment(email)}_backup.json'));
  }

  Future<bool> writeBackup({
    required String email,
    required List<ProfitEntry> entries,
  }) async {
    try {
      final file = await _backupFile(email);
      final payload = {
        'version': 2,
        'backupEmail': _normalizeEmail(email),
        'updatedAt': DateTime.now().toIso8601String(),
        'entriesCount': entries.length,
        'entries': entries.map((entry) => entry.toBackupMap()).toList(),
      };
      await file.writeAsString(
        const JsonEncoder.withIndent('  ').convert(payload),
        flush: true,
      );
      _lastError = null;
      return true;
    } catch (e) {
      _lastError = e.toString();
      return false;
    }
  }

  Future<BackupSnapshotInfo> readInfo(String email) async {
    try {
      final file = await _backupFile(email);
      if (!await file.exists()) {
        _lastError = null;
        return const BackupSnapshotInfo(exists: false);
      }
      final raw = await file.readAsString();
      if (raw.trim().isEmpty) {
        _lastError = null;
        return const BackupSnapshotInfo(exists: false);
      }
      final decoded = jsonDecode(raw) as Map<String, dynamic>;
      final entries = decoded['entries'];
      final updatedAtText = decoded['updatedAt'] as String?;
      _lastError = null;
      return BackupSnapshotInfo(
        exists: true,
        email: decoded['backupEmail'] as String?,
        updatedAt: updatedAtText == null ? null : DateTime.tryParse(updatedAtText),
        entriesCount: entries is List ? entries.length : 0,
        filePath: file.path,
      );
    } catch (e) {
      _lastError = e.toString();
      return const BackupSnapshotInfo(exists: false);
    }
  }

  Future<bool> restoreIfDatabaseEmpty(String expectedEmail) async {
    try {
      final currentCount = await DatabaseHelper.instance.countEntries();
      if (currentCount > 0) return false;

      final file = await _backupFile(expectedEmail);
      if (!await file.exists()) return false;

      final raw = await file.readAsString();
      if (raw.trim().isEmpty) return false;

      final decoded = jsonDecode(raw) as Map<String, dynamic>;
      final backupEmail = (decoded['backupEmail'] as String?)?.trim().toLowerCase();
      if (backupEmail != _normalizeEmail(expectedEmail)) {
        return false;
      }

      final entriesRaw = decoded['entries'];
      if (entriesRaw is! List || entriesRaw.isEmpty) return false;

      final entries = entriesRaw
          .whereType<Map>()
          .map((entry) => ProfitEntry.fromBackupMap(Map<String, dynamic>.from(entry)))
          .toList();

      if (entries.isEmpty) return false;

      await DatabaseHelper.instance.replaceAllEntries(entries);
      _lastError = null;
      return true;
    } catch (e) {
      _lastError = e.toString();
      return false;
    }
  }

  Future<File?> exportFile({
    required String email,
    required List<ProfitEntry> entries,
  }) async {
    final ok = await writeBackup(email: email, entries: entries);
    if (!ok) return null;
    return _backupFile(email);
  }
}


class CloudBackupSnapshotInfo {
  final bool exists;
  final String? email;
  final DateTime? updatedAt;
  final int entriesCount;

  const CloudBackupSnapshotInfo({
    required this.exists,
    this.email,
    this.updatedAt,
    this.entriesCount = 0,
  });
}

class CloudBackupService {
  CloudBackupService._();

  static final CloudBackupService instance = CloudBackupService._();

  bool _initialized = false;
  bool _available = false;
  String? _lastError;

  bool get isAvailable => _available;
  String? get lastError => _lastError;

  String _normalizeEmail(String email) => email.trim().toLowerCase();

  Future<void> init() async {
    if (_initialized) return;
    _initialized = true;
    await FirebaseBootstrap.ensureInitialized();
    _available = FirebaseBootstrap.isAvailable;
    _lastError = FirebaseBootstrap.lastError;
  }

  Future<bool> writeBackup({
    required String email,
    required List<ProfitEntry> entries,
  }) async {
    await init();
    if (!_available) return false;

    try {
      await FirebaseFirestore.instance
          .collection('profit_tracker_backups')
          .doc(_normalizeEmail(email))
          .set({
        'backupEmail': _normalizeEmail(email),
        'updatedAtIso': DateTime.now().toIso8601String(),
        'updatedAt': FieldValue.serverTimestamp(),
        'entriesCount': entries.length,
        'entries': entries.map((entry) => entry.toBackupMap()).toList(),
      }, SetOptions(merge: true));
      _lastError = null;
      return true;
    } catch (e) {
      _lastError = e.toString();
      return false;
    }
  }

  Future<CloudBackupSnapshotInfo> readInfo(String email) async {
    await init();
    if (!_available) return const CloudBackupSnapshotInfo(exists: false);

    try {
      final snapshot = await FirebaseFirestore.instance
          .collection('profit_tracker_backups')
          .doc(_normalizeEmail(email))
          .get();
      if (!snapshot.exists) {
        _lastError = null;
        return const CloudBackupSnapshotInfo(exists: false);
      }
      final data = snapshot.data();
      if (data == null) return const CloudBackupSnapshotInfo(exists: false);
      DateTime? updatedAt;
      final updatedAtValue = data['updatedAt'];
      if (updatedAtValue is Timestamp) {
        updatedAt = updatedAtValue.toDate();
      } else {
        final updatedAtIso = data['updatedAtIso'] as String?;
        if (updatedAtIso != null) updatedAt = DateTime.tryParse(updatedAtIso);
      }
      final entries = data['entries'];
      _lastError = null;
      return CloudBackupSnapshotInfo(
        exists: true,
        email: data['backupEmail'] as String?,
        updatedAt: updatedAt,
        entriesCount: entries is List ? entries.length : 0,
      );
    } catch (e) {
      _lastError = e.toString();
      return const CloudBackupSnapshotInfo(exists: false);
    }
  }

  Future<bool> restoreIfDatabaseEmpty(String email) async {
    final currentCount = await DatabaseHelper.instance.countEntries();
    if (currentCount > 0) return false;

    await init();
    if (!_available) return false;

    try {
      final snapshot = await FirebaseFirestore.instance
          .collection('profit_tracker_backups')
          .doc(_normalizeEmail(email))
          .get();
      if (!snapshot.exists) return false;
      final data = snapshot.data();
      if (data == null) return false;
      final entriesRaw = data['entries'];
      if (entriesRaw is! List || entriesRaw.isEmpty) return false;
      final entries = entriesRaw
          .whereType<Map>()
          .map((entry) => ProfitEntry.fromBackupMap(Map<String, dynamic>.from(entry)))
          .toList();
      if (entries.isEmpty) return false;
      await DatabaseHelper.instance.replaceAllEntries(entries);
      _lastError = null;
      return true;
    } catch (e) {
      _lastError = e.toString();
      return false;
    }
  }
}

class ManualMarketSettings {
  final double? gold;
  final double? silver;
  final double? eurUsd;

  const ManualMarketSettings({this.gold, this.silver, this.eurUsd});

  bool get hasAny => gold != null || silver != null || eurUsd != null;
  bool get isComplete => gold != null && silver != null && eurUsd != null;

  MarketSnapshot toSnapshot() {
    final now = DateTime.now();
    return MarketSnapshot(
      gold: MarketQuote(
        title: 'سعر الذهب',
        symbol: 'XAU',
        value: gold ?? 0,
        unit: 'دولار / أونصة',
        sourceUpdatedAt: now,
        sourceLabel: 'قيمة يدوية',
        icon: Icons.workspace_premium_outlined,
        color: const Color(0xFFC79A1B),
      ),
      silver: MarketQuote(
        title: 'سعر الفضة',
        symbol: 'XAG',
        value: silver ?? 0,
        unit: 'دولار / أونصة',
        sourceUpdatedAt: now,
        sourceLabel: 'قيمة يدوية',
        icon: Icons.brightness_5_outlined,
        color: const Color(0xFF6B7A90),
      ),
      eurUsd: MarketQuote(
        title: 'اليورو مقابل الدولار',
        symbol: 'EUR/USD',
        value: eurUsd ?? 0,
        unit: 'دولار لكل يورو',
        sourceUpdatedAt: now,
        sourceLabel: 'قيمة يدوية',
        icon: Icons.currency_exchange,
        color: const Color(0xFF1669C1),
      ),
      fetchedAt: now,
    );
  }
}

class MarketSettingsStore {
  static const _manualGoldKey = 'manual_market_gold';
  static const _manualSilverKey = 'manual_market_silver';
  static const _manualEurUsdKey = 'manual_market_eur_usd';
  static const _cachedSnapshotKey = 'cached_market_snapshot';

  Future<ManualMarketSettings> loadManual() async {
    final prefs = await SharedPreferences.getInstance();
    return ManualMarketSettings(
      gold: prefs.getDouble(_manualGoldKey),
      silver: prefs.getDouble(_manualSilverKey),
      eurUsd: prefs.getDouble(_manualEurUsdKey),
    );
  }

  Future<void> saveManual(ManualMarketSettings settings) async {
    final prefs = await SharedPreferences.getInstance();
    if (settings.gold == null) {
      await prefs.remove(_manualGoldKey);
    } else {
      await prefs.setDouble(_manualGoldKey, settings.gold!);
    }
    if (settings.silver == null) {
      await prefs.remove(_manualSilverKey);
    } else {
      await prefs.setDouble(_manualSilverKey, settings.silver!);
    }
    if (settings.eurUsd == null) {
      await prefs.remove(_manualEurUsdKey);
    } else {
      await prefs.setDouble(_manualEurUsdKey, settings.eurUsd!);
    }
  }

  Future<void> saveCachedSnapshot(MarketSnapshot snapshot) async {
    final prefs = await SharedPreferences.getInstance();
    final payload = {
      'fetchedAt': snapshot.fetchedAt.toIso8601String(),
      'gold': snapshot.gold.value,
      'silver': snapshot.silver.value,
      'eurUsd': snapshot.eurUsd.value,
      'goldUpdatedAt': snapshot.gold.sourceUpdatedAt?.toIso8601String(),
      'silverUpdatedAt': snapshot.silver.sourceUpdatedAt?.toIso8601String(),
      'eurUpdatedAt': snapshot.eurUsd.sourceUpdatedAt?.toIso8601String(),
    };
    await prefs.setString(_cachedSnapshotKey, jsonEncode(payload));
  }

  Future<MarketSnapshot?> loadCachedSnapshot() async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_cachedSnapshotKey);
    if (raw == null || raw.trim().isEmpty) return null;
    try {
      final map = jsonDecode(raw) as Map<String, dynamic>;
      DateTime? parseDate(String key) {
        final value = map[key] as String?;
        return value == null ? null : DateTime.tryParse(value);
      }
      final fetchedAt = parseDate('fetchedAt') ?? DateTime.now();
      final gold = (map['gold'] as num?)?.toDouble();
      final silver = (map['silver'] as num?)?.toDouble();
      final eurUsd = (map['eurUsd'] as num?)?.toDouble();
      if (gold == null || silver == null || eurUsd == null) return null;
      return MarketSnapshot(
        gold: MarketQuote(
          title: 'سعر الذهب',
          symbol: 'XAU',
          value: gold,
          unit: 'دولار / أونصة',
          sourceUpdatedAt: parseDate('goldUpdatedAt'),
          sourceLabel: 'آخر بيانات ناجحة',
          icon: Icons.workspace_premium_outlined,
          color: const Color(0xFFC79A1B),
        ),
        silver: MarketQuote(
          title: 'سعر الفضة',
          symbol: 'XAG',
          value: silver,
          unit: 'دولار / أونصة',
          sourceUpdatedAt: parseDate('silverUpdatedAt'),
          sourceLabel: 'آخر بيانات ناجحة',
          icon: Icons.brightness_5_outlined,
          color: const Color(0xFF6B7A90),
        ),
        eurUsd: MarketQuote(
          title: 'اليورو مقابل الدولار',
          symbol: 'EUR/USD',
          value: eurUsd,
          unit: 'دولار لكل يورو',
          sourceUpdatedAt: parseDate('eurUpdatedAt'),
          sourceLabel: 'آخر بيانات ناجحة',
          icon: Icons.currency_exchange,
          color: const Color(0xFF1669C1),
        ),
        fetchedAt: fetchedAt,
      );
    } catch (_) {
      return null;
    }
  }
}

class MarketQuote {
  final String title;
  final String symbol;
  final double value;
  final String unit;
  final DateTime? sourceUpdatedAt;
  final String sourceLabel;
  final IconData icon;
  final Color color;

  const MarketQuote({
    required this.title,
    required this.symbol,
    required this.value,
    required this.unit,
    required this.sourceUpdatedAt,
    required this.sourceLabel,
    required this.icon,
    required this.color,
  });
}

class MarketSnapshot {
  final MarketQuote gold;
  final MarketQuote silver;
  final MarketQuote eurUsd;
  final DateTime fetchedAt;

  const MarketSnapshot({
    required this.gold,
    required this.silver,
    required this.eurUsd,
    required this.fetchedAt,
  });

  List<MarketQuote> get quotes => [gold, silver, eurUsd];
}

class MarketService {
  MarketService._();

  static final http.Client _client = http.Client();

  static Future<MarketSnapshot> fetchSnapshot() async {
    final results = await Future.wait<MarketQuote>([
      _fetchMetalQuote(
        symbol: 'XAU',
        title: 'سعر الذهب',
        unit: 'دولار / أونصة',
        sourceLabel: 'Gold API',
        icon: Icons.workspace_premium_outlined,
        color: const Color(0xFFC79A1B),
      ),
      _fetchMetalQuote(
        symbol: 'XAG',
        title: 'سعر الفضة',
        unit: 'دولار / أونصة',
        sourceLabel: 'Gold API',
        icon: Icons.brightness_5_outlined,
        color: const Color(0xFF6B7A90),
      ),
      _fetchEurUsdQuote(),
    ]);

    return MarketSnapshot(
      gold: results[0],
      silver: results[1],
      eurUsd: results[2],
      fetchedAt: DateTime.now(),
    );
  }

  static Future<MarketQuote> _fetchMetalQuote({
    required String symbol,
    required String title,
    required String unit,
    required String sourceLabel,
    required IconData icon,
    required Color color,
  }) async {
    final response = await _client
        .get(
          Uri.parse('https://api.gold-api.com/price/$symbol'),
          headers: const {'Accept': 'application/json', 'User-Agent': 'profit-tracker-app/1.0'},
        )
        .timeout(const Duration(seconds: 12));

    if (response.statusCode != 200) {
      throw HttpException('metal quote status ${response.statusCode}');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('invalid metal quote payload');
    }

    final rawPrice = decoded['price'];
    if (rawPrice is! num) {
      throw const FormatException('missing metal price');
    }

    DateTime? updatedAt;
    final updatedText = decoded['updatedAt'] as String?;
    if (updatedText != null && updatedText.trim().isNotEmpty) {
      updatedAt = DateTime.tryParse(updatedText);
    }

    return MarketQuote(
      title: title,
      symbol: symbol,
      value: rawPrice.toDouble(),
      unit: unit,
      sourceUpdatedAt: updatedAt,
      sourceLabel: sourceLabel,
      icon: icon,
      color: color,
    );
  }

  static Future<MarketQuote> _fetchEurUsdQuote() async {
    try {
      final response = await _client
          .get(
            Uri.parse('https://api.frankfurter.dev/v2/rate/EUR/USD'),
            headers: const {'Accept': 'application/json', 'User-Agent': 'profit-tracker-app/1.0'},
          )
          .timeout(const Duration(seconds: 12));

      if (response.statusCode == 200) {
        final decoded = jsonDecode(response.body);
        if (decoded is Map<String, dynamic>) {
          final rate = decoded['rate'];
          if (rate is num) {
            final dateText = decoded['date'] as String?;
            return MarketQuote(
              title: 'اليورو مقابل الدولار',
              symbol: 'EUR/USD',
              value: rate.toDouble(),
              unit: 'دولار لكل يورو',
              sourceUpdatedAt: dateText == null ? null : DateTime.tryParse(dateText),
              sourceLabel: 'Frankfurter',
              icon: Icons.currency_exchange,
              color: const Color(0xFF1669C1),
            );
          }
        }
      }
    } catch (_) {
      // Fall back to the v1 endpoint below.
    }

    final fallback = await _client
        .get(
          Uri.parse('https://api.frankfurter.dev/v1/latest?base=EUR&symbols=USD'),
          headers: const {'Accept': 'application/json', 'User-Agent': 'profit-tracker-app/1.0'},
        )
        .timeout(const Duration(seconds: 12));

    if (fallback.statusCode != 200) {
      throw HttpException('fx quote status ${fallback.statusCode}');
    }

    final decoded = jsonDecode(fallback.body);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('invalid fx quote payload');
    }

    final rates = decoded['rates'];
    if (rates is! Map<String, dynamic>) {
      throw const FormatException('missing fx rates');
    }

    final rate = rates['USD'];
    if (rate is! num) {
      throw const FormatException('missing EUR/USD rate');
    }

    final dateText = decoded['date'] as String?;
    return MarketQuote(
      title: 'اليورو مقابل الدولار',
      symbol: 'EUR/USD',
      value: rate.toDouble(),
      unit: 'دولار لكل يورو',
      sourceUpdatedAt: dateText == null ? null : DateTime.tryParse(dateText),
      sourceLabel: 'Frankfurter',
      icon: Icons.currency_exchange,
      color: const Color(0xFF1669C1),
    );
  }
}



class LocationWeatherSnapshot {
  final double latitude;
  final double longitude;
  final double temperatureC;
  final double apparentTemperatureC;
  final double windSpeedKmH;
  final int weatherCode;
  final bool isDay;
  final String description;
  final String timezone;
  final String timezoneAbbreviation;
  final int utcOffsetSeconds;
  final DateTime weatherTime;
  final DateTime fetchedAt;

  const LocationWeatherSnapshot({
    required this.latitude,
    required this.longitude,
    required this.temperatureC,
    required this.apparentTemperatureC,
    required this.windSpeedKmH,
    required this.weatherCode,
    required this.isDay,
    required this.description,
    required this.timezone,
    required this.timezoneAbbreviation,
    required this.utcOffsetSeconds,
    required this.weatherTime,
    required this.fetchedAt,
  });

  Map<String, Object?> toJson() {
    return {
      'latitude': latitude,
      'longitude': longitude,
      'temperatureC': temperatureC,
      'apparentTemperatureC': apparentTemperatureC,
      'windSpeedKmH': windSpeedKmH,
      'weatherCode': weatherCode,
      'isDay': isDay,
      'description': description,
      'timezone': timezone,
      'timezoneAbbreviation': timezoneAbbreviation,
      'utcOffsetSeconds': utcOffsetSeconds,
      'weatherTime': weatherTime.toIso8601String(),
      'fetchedAt': fetchedAt.toIso8601String(),
    };
  }

  factory LocationWeatherSnapshot.fromJson(Map<String, dynamic> map) {
    return LocationWeatherSnapshot(
      latitude: (map['latitude'] as num).toDouble(),
      longitude: (map['longitude'] as num).toDouble(),
      temperatureC: (map['temperatureC'] as num).toDouble(),
      apparentTemperatureC: (map['apparentTemperatureC'] as num).toDouble(),
      windSpeedKmH: (map['windSpeedKmH'] as num).toDouble(),
      weatherCode: (map['weatherCode'] as num).toInt(),
      isDay: map['isDay'] == true,
      description: (map['description'] as String?) ?? '—',
      timezone: (map['timezone'] as String?) ?? '—',
      timezoneAbbreviation: (map['timezoneAbbreviation'] as String?) ?? '—',
      utcOffsetSeconds: (map['utcOffsetSeconds'] as num?)?.toInt() ?? 0,
      weatherTime: DateTime.tryParse((map['weatherTime'] as String?) ?? '') ?? DateTime.now(),
      fetchedAt: DateTime.tryParse((map['fetchedAt'] as String?) ?? '') ?? DateTime.now(),
    );
  }
}

class LocationWeatherStore {
  static const _cachedSnapshotKey = 'cached_location_weather_snapshot';

  Future<void> saveCachedSnapshot(LocationWeatherSnapshot snapshot) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_cachedSnapshotKey, jsonEncode(snapshot.toJson()));
  }

  Future<LocationWeatherSnapshot?> loadCachedSnapshot() async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_cachedSnapshotKey);
    if (raw == null || raw.trim().isEmpty) return null;
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! Map<String, dynamic>) return null;
      return LocationWeatherSnapshot.fromJson(decoded);
    } catch (_) {
      return null;
    }
  }
}

class _ApproxInternetLocation {
  final double latitude;
  final double longitude;
  final String timezone;
  final String timezoneAbbreviation;
  final int utcOffsetSeconds;

  const _ApproxInternetLocation({
    required this.latitude,
    required this.longitude,
    required this.timezone,
    required this.timezoneAbbreviation,
    required this.utcOffsetSeconds,
  });
}

class DeviceLocationWeatherException implements Exception {
  final String message;

  const DeviceLocationWeatherException(this.message);

  @override
  String toString() => message;
}

class DeviceLocationWeatherService {
  DeviceLocationWeatherService._();

  static final http.Client _client = http.Client();

  static Future<LocationWeatherSnapshot> fetchSnapshot() async {
    final approx = await _resolveApproxLocationFromInternet();

    final uri = Uri.parse(
      'https://api.open-meteo.com/v1/forecast'
      '?latitude=${approx.latitude}'
      '&longitude=${approx.longitude}'
      '&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,is_day'
      '&timezone=auto',
    );

    final response = await _client.get(
      uri,
      headers: const {
        'Accept': 'application/json',
        'User-Agent': 'profit-tracker-app/1.0',
      },
    ).timeout(const Duration(seconds: 15));

    if (response.statusCode != 200) {
      throw DeviceLocationWeatherException(
        'تعذّر تحميل الطقس الحالي من الإنترنت (رمز الاستجابة: ${response.statusCode}).',
      );
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const DeviceLocationWeatherException('تم استلام بيانات طقس غير صالحة.');
    }

    final current = decoded['current'];
    if (current is! Map<String, dynamic>) {
      throw const DeviceLocationWeatherException('الخدمة لم ترجع بيانات الطقس الحالية.');
    }

    final temperature = current['temperature_2m'];
    final apparent = current['apparent_temperature'];
    final windSpeed = current['wind_speed_10m'];
    final weatherCode = current['weather_code'];
    if (temperature is! num || apparent is! num || windSpeed is! num || weatherCode is! num) {
      throw const DeviceLocationWeatherException('بيانات الطقس الحالية ناقصة أو غير مكتملة.');
    }

    final isDayValue = current['is_day'];
    final isDay = isDayValue is num ? isDayValue.toInt() == 1 : true;
    final weatherTime = DateTime.tryParse((current['time'] as String?) ?? '') ?? DateTime.now();
    final timezone = (decoded['timezone'] as String?) ?? approx.timezone;
    final timezoneAbbreviation =
        (decoded['timezone_abbreviation'] as String?) ?? approx.timezoneAbbreviation;
    final utcOffsetSeconds =
        (decoded['utc_offset_seconds'] as num?)?.toInt() ?? approx.utcOffsetSeconds;
    final code = weatherCode.toInt();

    return LocationWeatherSnapshot(
      latitude: approx.latitude,
      longitude: approx.longitude,
      temperatureC: temperature.toDouble(),
      apparentTemperatureC: apparent.toDouble(),
      windSpeedKmH: windSpeed.toDouble(),
      weatherCode: code,
      isDay: isDay,
      description: _weatherCodeDescription(code, isDay: isDay),
      timezone: timezone,
      timezoneAbbreviation: timezoneAbbreviation,
      utcOffsetSeconds: utcOffsetSeconds,
      weatherTime: weatherTime,
      fetchedAt: DateTime.now(),
    );
  }

  static Future<_ApproxInternetLocation> _resolveApproxLocationFromInternet() async {
    final uri = Uri.parse('https://ipwho.is/');
    final response = await _client.get(
      uri,
      headers: const {
        'Accept': 'application/json',
        'User-Agent': 'profit-tracker-app/1.0',
      },
    ).timeout(const Duration(seconds: 12));

    if (response.statusCode != 200) {
      throw DeviceLocationWeatherException(
        'تعذّر تحديد المنطقة التقريبية من الإنترنت (رمز الاستجابة: ${response.statusCode}).',
      );
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const DeviceLocationWeatherException(
        'تم استلام بيانات منطقة غير صالحة من الإنترنت.',
      );
    }

    final success = decoded['success'];
    if (success == false) {
      throw const DeviceLocationWeatherException(
        'تعذّر تحديد المنطقة التقريبية من الإنترنت حاليًا.',
      );
    }

    final latitude = decoded['latitude'];
    final longitude = decoded['longitude'];
    if (latitude is! num || longitude is! num) {
      throw const DeviceLocationWeatherException(
        'تعذّر تحديد المنطقة التقريبية من الإنترنت حاليًا.',
      );
    }

    final timezoneMap = decoded['timezone'];
    String timezone = 'UTC';
    String timezoneAbbreviation = 'UTC';
    int utcOffsetSeconds = 0;
    if (timezoneMap is Map<String, dynamic>) {
      timezone = (timezoneMap['id'] as String?) ?? timezone;
      timezoneAbbreviation = (timezoneMap['abbr'] as String?) ?? timezoneAbbreviation;
      final offset = timezoneMap['offset'];
      if (offset is num) {
        utcOffsetSeconds = offset.toInt();
      }
    }

    return _ApproxInternetLocation(
      latitude: latitude.toDouble(),
      longitude: longitude.toDouble(),
      timezone: timezone,
      timezoneAbbreviation: timezoneAbbreviation,
      utcOffsetSeconds: utcOffsetSeconds,
    );
  }

  static String _weatherCodeDescription(int code, {required bool isDay}) {
    switch (code) {
      case 0:
        return isDay ? 'سماء صافية' : 'سماء صافية ليلًا';
      case 1:
        return isDay ? 'صحو غالبًا' : 'سماء شبه صافية';
      case 2:
        return 'غائم جزئيًا';
      case 3:
        return 'غائم';
      case 45:
      case 48:
        return 'ضباب';
      case 51:
      case 53:
      case 55:
        return 'رذاذ';
      case 56:
      case 57:
        return 'رذاذ متجمد';
      case 61:
      case 63:
      case 65:
        return 'أمطار';
      case 66:
      case 67:
        return 'أمطار متجمدة';
      case 71:
      case 73:
      case 75:
        return 'ثلوج';
      case 77:
        return 'حبوب ثلج';
      case 80:
      case 81:
      case 82:
        return 'زخات مطر';
      case 85:
      case 86:
        return 'زخات ثلج';
      case 95:
        return 'عاصفة رعدية';
      case 96:
      case 99:
        return 'عاصفة رعدية مع برد';
      default:
        return 'حالة طقس غير معروفة';
    }
  }
}

class AppStartupGate extends StatefulWidget {
  const AppStartupGate({super.key});

  @override
  State<AppStartupGate> createState() => _AppStartupGateState();
}

class _AppStartupGateState extends State<AppStartupGate> {
  static const Duration _startupTimeout = Duration(seconds: 5);

  final ProfileStore _profileStore = ProfileStore();
  bool _loading = true;
  bool _firebaseAvailable = false;
  String? _firebaseError;
  String? _userEmail;
  String? _prefillEmail;
  String? _startupMessage;

  @override
  void initState() {
    super.initState();
    _initialize();
  }

  Future<void> _initialize() async {
    final profile = await _profileStore.load();
    String? signedInEmail;
    String? message;

    try {
      await AuthService.instance.init().timeout(_startupTimeout);
    } catch (e) {
      _firebaseError = 'تعذر تهيئة Firebase الآن: $e';
    }

    final firebaseEmail = AuthService.instance.currentUserEmail?.trim().toLowerCase();
    if (AuthService.instance.isAvailable) {
      if (firebaseEmail != null && firebaseEmail.isNotEmpty) {
        signedInEmail = firebaseEmail;
      } else {
        signedInEmail = null;
        if ((profile?.email ?? '').isNotEmpty) {
          message = 'يرجى تسجيل الدخول بنفس الإيميل لتفعيل النسخة السحابية والاسترجاع على أي جهاز.';
        }
      }
    } else {
      signedInEmail = profile?.email;
    }

    if (signedInEmail != null && signedInEmail.isNotEmpty) {
      try {
        final restoredLocal = await BackupService.instance
            .restoreIfDatabaseEmpty(signedInEmail)
            .timeout(_startupTimeout, onTimeout: () => false);
        if (restoredLocal) {
          message = 'تمت استعادة النسخة الاحتياطية المحلية تلقائيًا.';
        } else if (AuthService.instance.isAvailable) {
          final restoredCloud = await CloudBackupService.instance
              .restoreIfDatabaseEmpty(signedInEmail)
              .timeout(_startupTimeout, onTimeout: () => false);
          if (restoredCloud) {
            final entries = await DatabaseHelper.instance.getAllEntries();
            await BackupService.instance.writeBackup(email: signedInEmail, entries: entries);
            message = 'تمت استعادة النسخة السحابية تلقائيًا.';
          }
        }
      } catch (_) {
        message ??= 'تم فتح البرنامج بالوضع المحلي، ويمكن إكمال المزامنة لاحقًا.';
      }
      await _profileStore.saveEmail(signedInEmail);
    }

    if (!mounted) return;
    setState(() {
      _firebaseAvailable = AuthService.instance.isAvailable;
      _firebaseError = _firebaseError ?? AuthService.instance.lastError;
      _userEmail = signedInEmail;
      _prefillEmail = profile?.email ?? firebaseEmail;
      _startupMessage = message ??
          (!AuthService.instance.isAvailable && signedInEmail != null && signedInEmail.isNotEmpty
              ? 'تم فتح البرنامج بالوضع المحلي. فعّل Firebase لاحقًا للمزامنة التلقائية بين الأجهزة.'
              : null);
      _loading = false;
    });
  }

  Future<void> _continueLocally(String email) async {
    final normalized = email.trim().toLowerCase();
    await _profileStore.saveEmail(normalized);
    final restoredLocal = await BackupService.instance.restoreIfDatabaseEmpty(normalized);
    if (!mounted) return;
    setState(() {
      _userEmail = normalized;
      _prefillEmail = normalized;
      _startupMessage = restoredLocal
          ? 'تم فتح البرنامج واستعادة النسخة المحلية تلقائيًا.'
          : 'تم فتح البرنامج بالوضع المحلي على هذا الجهاز.';
    });
  }

  Future<AuthResult> _authenticate({
    required String email,
    required String password,
    required bool register,
  }) async {
    final result = register
        ? await AuthService.instance.register(email: email, password: password)
        : await AuthService.instance.signIn(email: email, password: password);

    if (!result.success) return result;

    final normalized = (result.email ?? email).trim().toLowerCase();
    await _profileStore.saveEmail(normalized);

    String message;
    final currentCount = await DatabaseHelper.instance.countEntries();
    if (currentCount == 0) {
      final restoredLocal = await BackupService.instance.restoreIfDatabaseEmpty(normalized);
      var restoredCloud = false;
      if (!restoredLocal) {
        restoredCloud = await CloudBackupService.instance.restoreIfDatabaseEmpty(normalized);
      }
      final entriesAfterRestore = await DatabaseHelper.instance.getAllEntries();
      await BackupService.instance.writeBackup(email: normalized, entries: entriesAfterRestore);
      final cloudSaved = await CloudBackupService.instance.writeBackup(
        email: normalized,
        entries: entriesAfterRestore,
      );
      if (restoredCloud) {
        message = 'تم تسجيل الدخول واستعادة البيانات السحابية تلقائيًا.';
      } else if (restoredLocal) {
        message = 'تم تسجيل الدخول واستعادة النسخة المحلية تلقائيًا.';
      } else if (cloudSaved) {
        message = result.isNewAccount
            ? 'تم إنشاء الحساب وتجهيز النسخة المحلية والسحابية.'
            : 'تم تسجيل الدخول وتجهيز النسخة المحلية والسحابية.';
      } else {
        message = result.isNewAccount
            ? 'تم إنشاء الحساب وتجهيز النسخة المحلية. أكمل إعداد Firestore للمزامنة السحابية.'
            : 'تم تسجيل الدخول وتجهيز النسخة المحلية. أكمل إعداد Firestore للمزامنة السحابية.';
      }
    } else {
      final entries = await DatabaseHelper.instance.getAllEntries();
      await BackupService.instance.writeBackup(email: normalized, entries: entries);
      final cloudSaved = await CloudBackupService.instance.writeBackup(
        email: normalized,
        entries: entries,
      );
      message = cloudSaved
          ? 'تم تسجيل الدخول ومزامنة البيانات المحلية إلى السحابة.'
          : 'تم تسجيل الدخول. البيانات المحلية محفوظة، لكن المزامنة السحابية تحتاج إكمال إعداد Firestore.';
    }

    if (!mounted) return result;
    setState(() {
      _userEmail = normalized;
      _prefillEmail = normalized;
      _startupMessage = message;
    });
    return result;
  }

  Future<void> _signOut() async {
    await AuthService.instance.signOut();
    await _profileStore.clear();
    if (!mounted) return;
    setState(() {
      _userEmail = null;
      _startupMessage = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const SplashLoadingScreen();

    if (_userEmail == null || _userEmail!.isEmpty) {
      if (_firebaseAvailable) {
        return BackupAuthGate(
          initialEmail: _prefillEmail,
          onAuthenticate: _authenticate,
        );
      }
      return LocalEmailGate(
        initialEmail: _prefillEmail,
        errorMessage: _firebaseError,
        onContinue: _continueLocally,
      );
    }

    return ProfitHomePage(
      userEmail: _userEmail!,
      startupMessage: _startupMessage,
      onSignOut: _signOut,
    );
  }
}

class SplashLoadingScreen extends StatelessWidget {
  const SplashLoadingScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircleAvatar(
              radius: 34,
              backgroundColor: scheme.primary.withValues(alpha: 0.12),
              child: Icon(Icons.account_balance_wallet, size: 34, color: scheme.primary),
            ),
            const SizedBox(height: 18),
            const Text(
              'جارٍ تجهيز Maen Accountings',
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 10),
            const SizedBox(
              width: 30,
              height: 30,
              child: CircularProgressIndicator(strokeWidth: 3),
            ),
          ],
        ),
      ),
    );
  }
}


class LocalEmailGate extends StatefulWidget {
  final String? initialEmail;
  final String? errorMessage;
  final Future<void> Function(String email) onContinue;

  const LocalEmailGate({
    super.key,
    required this.onContinue,
    this.initialEmail,
    this.errorMessage,
  });

  @override
  State<LocalEmailGate> createState() => _LocalEmailGateState();
}

class _LocalEmailGateState extends State<LocalEmailGate> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  late final TextEditingController _emailController;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _emailController = TextEditingController(text: widget.initialEmail ?? '');
  }

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    await widget.onContinue(_emailController.text);
    if (!mounted) return;
    setState(() => _saving = false);
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      body: Container(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [
              scheme.primary.withValues(alpha: 0.08),
              Theme.of(context).scaffoldBackgroundColor,
            ],
          ),
        ),
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 560),
              child: Card(
                child: Padding(
                  padding: const EdgeInsets.all(22),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        CircleAvatar(
                          radius: 28,
                          backgroundColor: scheme.primary.withValues(alpha: 0.12),
                          child: Icon(Icons.phone_android, size: 30, color: scheme.primary),
                        ),
                        const SizedBox(height: 16),
                        const Text(
                          'فتح محلي على هذا الجهاز',
                          style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
                        ),
                        const SizedBox(height: 10),
                        Text(
                          widget.errorMessage == null || widget.errorMessage!.isEmpty
                              ? 'المزامنة السحابية غير متاحة الآن. أدخل الإيميل للمتابعة محليًا، وسيتم حفظ بياناتك داخل هذا الجهاز إلى أن تكتمل المزامنة لاحقًا.'
                              : 'تعذر تهيئة المزامنة السحابية الآن. أدخل الإيميل للمتابعة محليًا على هذا الجهاز.\n\nالسبب: ${widget.errorMessage}',
                          style: const TextStyle(height: 1.6),
                        ),
                        const SizedBox(height: 18),
                        TextFormField(
                          controller: _emailController,
                          keyboardType: TextInputType.emailAddress,
                          decoration: const InputDecoration(
                            labelText: 'الإيميل',
                            prefixIcon: Icon(Icons.email_outlined),
                          ),
                          validator: (value) {
                            final text = value?.trim() ?? '';
                            if (text.isEmpty) return 'أدخل الإيميل أولًا';
                            final emailRegExp = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
                            if (!emailRegExp.hasMatch(text)) return 'أدخل إيميل صحيح';
                            return null;
                          },
                        ),
                        const SizedBox(height: 16),
                        SizedBox(
                          width: double.infinity,
                          child: FilledButton.icon(
                            onPressed: _saving ? null : _submit,
                            icon: _saving
                                ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  )
                                : const Icon(Icons.arrow_forward),
                            label: const Text('متابعة وفتح البرنامج'),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class FirebaseSetupRequiredScreen extends StatelessWidget {
  final String? errorMessage;

  const FirebaseSetupRequiredScreen({super.key, this.errorMessage});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: Card(
              child: Padding(
                padding: const EdgeInsets.all(22),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.cloud_off_outlined, size: 42),
                    const SizedBox(height: 12),
                    const Text(
                      'يلزم إكمال إعداد Firebase أولًا',
                      style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 10),
                    const Text(
                      'هذه النسخة مجهزة لتسجيل الدخول بالإيميل وكلمة المرور، والمزامنة السحابية التلقائية بين الأجهزة عبر Firebase Authentication وCloud Firestore.',
                      style: TextStyle(height: 1.6),
                    ),
                    const SizedBox(height: 12),
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color: Colors.orange.withValues(alpha: 0.08),
                        borderRadius: BorderRadius.circular(14),
                      ),
                      child: Text(
                        errorMessage ?? 'نفّذ flutterfire configure ثم فعّل Email/Password في Authentication وأنشئ Cloud Firestore.',
                        style: const TextStyle(height: 1.6),
                      ),
                    ),
                    const SizedBox(height: 14),
                    const Text(
                      'بعد إكمال الإعداد، سيعمل البرنامج على حفظ البيانات محليًا داخل الجهاز ومزامنتها تلقائيًا على السحابة، وعند تسجيل الدخول من جهاز آخر ستعود البيانات تلقائيًا.',
                      style: TextStyle(height: 1.6),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class BackupAuthGate extends StatefulWidget {
  final String? initialEmail;
  final Future<AuthResult> Function({
    required String email,
    required String password,
    required bool register,
  }) onAuthenticate;

  const BackupAuthGate({
    super.key,
    required this.onAuthenticate,
    this.initialEmail,
  });

  @override
  State<BackupAuthGate> createState() => _BackupAuthGateState();
}

class _BackupAuthGateState extends State<BackupAuthGate> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  late final TextEditingController _emailController;
  final TextEditingController _passwordController = TextEditingController();
  final TextEditingController _confirmPasswordController = TextEditingController();
  bool _saving = false;
  bool _registerMode = false;
  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;

  @override
  void initState() {
    super.initState();
    _emailController = TextEditingController(text: widget.initialEmail ?? '');
  }

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  bool _isValidEmail(String value) {
    final emailRegExp = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    return emailRegExp.hasMatch(value.trim());
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    final result = await widget.onAuthenticate(
      email: _emailController.text.trim(),
      password: _passwordController.text,
      register: _registerMode,
    );
    if (!mounted) return;
    setState(() => _saving = false);
    if (!result.success) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(result.message)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      body: Container(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [
              scheme.primary.withValues(alpha: 0.08),
              Theme.of(context).scaffoldBackgroundColor,
            ],
          ),
        ),
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 560),
              child: Card(
                child: Padding(
                  padding: const EdgeInsets.all(22),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        CircleAvatar(
                          radius: 28,
                          backgroundColor: scheme.primary.withValues(alpha: 0.12),
                          child: Icon(Icons.cloud_done_outlined, size: 30, color: scheme.primary),
                        ),
                        const SizedBox(height: 16),
                        Text(
                          _registerMode ? 'إنشاء حساب ومزامنة تلقائية' : 'تسجيل الدخول قبل فتح البرنامج',
                          style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
                        ),
                        const SizedBox(height: 10),
                        const Text(
                          'سجّل دخولك ليتم حفظ بياناتك داخل الجهاز ومزامنتها تلقائيًا على السحابة. عند فتح التطبيق في جهاز آخر وتسجيل الدخول بنفس الحساب ستعود بياناتك تلقائيًا.',
                          style: TextStyle(height: 1.6),
                        ),
                        const SizedBox(height: 18),
                        SegmentedButton<bool>(
                          segments: const [
                            ButtonSegment<bool>(
                              value: false,
                              icon: Icon(Icons.login),
                              label: Text('تسجيل الدخول'),
                            ),
                            ButtonSegment<bool>(
                              value: true,
                              icon: Icon(Icons.person_add_alt_1),
                              label: Text('إنشاء حساب'),
                            ),
                          ],
                          selected: {_registerMode},
                          onSelectionChanged: (value) {
                            setState(() {
                              _registerMode = value.first;
                            });
                          },
                        ),
                        const SizedBox(height: 16),
                        TextFormField(
                          controller: _emailController,
                          keyboardType: TextInputType.emailAddress,
                          decoration: const InputDecoration(
                            labelText: 'الإيميل',
                            prefixIcon: Icon(Icons.email_outlined),
                          ),
                          validator: (value) {
                            final text = value?.trim() ?? '';
                            if (text.isEmpty) return 'أدخل الإيميل أولًا';
                            if (!_isValidEmail(text)) return 'أدخل إيميل صحيح';
                            return null;
                          },
                        ),
                        const SizedBox(height: 12),
                        TextFormField(
                          controller: _passwordController,
                          obscureText: _obscurePassword,
                          decoration: InputDecoration(
                            labelText: 'كلمة المرور',
                            prefixIcon: const Icon(Icons.lock_outline),
                            suffixIcon: IconButton(
                              onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                              icon: Icon(_obscurePassword ? Icons.visibility : Icons.visibility_off),
                            ),
                          ),
                          validator: (value) {
                            final text = value ?? '';
                            if (text.isEmpty) return 'أدخل كلمة المرور';
                            if (text.length < 6) return 'كلمة المرور يجب أن تكون 6 أحرف على الأقل';
                            return null;
                          },
                        ),
                        if (_registerMode) ...[
                          const SizedBox(height: 12),
                          TextFormField(
                            controller: _confirmPasswordController,
                            obscureText: _obscureConfirmPassword,
                            decoration: InputDecoration(
                              labelText: 'تأكيد كلمة المرور',
                              prefixIcon: const Icon(Icons.verified_user_outlined),
                              suffixIcon: IconButton(
                                onPressed: () => setState(() => _obscureConfirmPassword = !_obscureConfirmPassword),
                                icon: Icon(_obscureConfirmPassword ? Icons.visibility : Icons.visibility_off),
                              ),
                            ),
                            validator: (value) {
                              if (!_registerMode) return null;
                              if ((value ?? '').isEmpty) return 'أكد كلمة المرور';
                              if (value != _passwordController.text) return 'كلمتا المرور غير متطابقتين';
                              return null;
                            },
                          ),
                        ],
                        const SizedBox(height: 16),
                        SizedBox(
                          width: double.infinity,
                          child: FilledButton.icon(
                            onPressed: _saving ? null : _submit,
                            icon: _saving
                                ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  )
                                : Icon(_registerMode ? Icons.person_add_alt_1 : Icons.lock_open),
                            label: Text(_registerMode ? 'إنشاء الحساب وفتح البرنامج' : 'تسجيل الدخول وفتح البرنامج'),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class ProfitHomePage extends StatefulWidget {
  final String userEmail;
  final String? startupMessage;
  final Future<void> Function() onSignOut;

  const ProfitHomePage({
    super.key,
    required this.userEmail,
    required this.startupMessage,
    required this.onSignOut,
  });

  @override
  State<ProfitHomePage> createState() => _ProfitHomePageState();
}

class _ProfitHomePageState extends State<ProfitHomePage> {
  final _formKey = GlobalKey<FormState>();
  final TextEditingController _salesController = TextEditingController();
  final TextEditingController _costController = TextEditingController();
  final TextEditingController _expensesController = TextEditingController(text: '0');
  final TextEditingController _notesController = TextEditingController();
  final TextEditingController _searchController = TextEditingController();

  List<ProfitEntry> _entries = [];
  bool _loading = true;
  bool _saving = false;
  bool _sharingBackup = false;
  bool _cloudSyncing = false;
  bool _marketLoading = true;
  bool _marketRefreshing = false;
  int _selectedIndex = 0;
  DateTime _selectedDate = DateTime.now();
  DateTime _reportDate = DateTime.now();
  ProfitEntry? _editingEntry;
  Timer? _marketTimer;
  Timer? _clockTimer;
  String _userEmail = '';
  String? _marketError;
  String? _locationWeatherError;
  MarketSnapshot? _marketSnapshot;
  LocationWeatherSnapshot? _locationWeatherSnapshot;
  BackupSnapshotInfo _backupInfo = const BackupSnapshotInfo(exists: false);
  CloudBackupSnapshotInfo _cloudBackupInfo = const CloudBackupSnapshotInfo(exists: false);
  final MarketSettingsStore _marketSettingsStore = MarketSettingsStore();
  final LocationWeatherStore _locationWeatherStore = LocationWeatherStore();
  ManualMarketSettings _manualMarketSettings = const ManualMarketSettings();
  bool _locationWeatherLoading = true;
  bool _locationWeatherRefreshing = false;

  @override
  void initState() {
    super.initState();
    _userEmail = widget.userEmail;
    _searchController.addListener(_handleSearchChanged);
    _loadEntries(showStartupMessage: true);
    unawaited(_prepareMarket());
    unawaited(_prepareLocationWeather());
    _clockTimer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) {
        setState(() {});
      }
    });
    _marketTimer = Timer.periodic(
      const Duration(minutes: 5),
      (_) {
        unawaited(_refreshMarketData());
        unawaited(_refreshLocationWeather());
      },
    );
  }

  @override
  void dispose() {
    _salesController.dispose();
    _costController.dispose();
    _expensesController.dispose();
    _notesController.dispose();
    _marketTimer?.cancel();
    _clockTimer?.cancel();
    _searchController
      ..removeListener(_handleSearchChanged)
      ..dispose();
    super.dispose();
  }

  void _handleSearchChanged() {
    if (mounted) setState(() {});
  }

  Future<void> _prepareMarket() async {
    final manual = await _marketSettingsStore.loadManual();
    final cached = await _marketSettingsStore.loadCachedSnapshot();
    if (!mounted) return;
    setState(() {
      _manualMarketSettings = manual;
      if (cached != null) {
        _marketSnapshot = cached;
        _marketLoading = false;
      }
    });
    await _refreshMarketData(initialLoad: cached == null);
  }

  Future<void> _refreshMarketData({bool initialLoad = false}) async {
    if (_marketRefreshing) return;

    if (mounted) {
      setState(() {
        _marketRefreshing = true;
        if (initialLoad && _marketSnapshot == null) {
          _marketLoading = true;
        }
      });
    }

    try {
      final snapshot = await MarketService.fetchSnapshot();
      await _marketSettingsStore.saveCachedSnapshot(snapshot);
      if (!mounted) return;
      setState(() {
        _marketSnapshot = snapshot;
        _marketError = null;
        _marketLoading = false;
        _marketRefreshing = false;
      });
    } catch (e) {
      final cached = await _marketSettingsStore.loadCachedSnapshot();
      if (!mounted) return;
      setState(() {
        if (cached != null) {
          _marketSnapshot = cached;
          _marketError = 'تعذّر التحديث عبر الإنترنت، ويجري الآن عرض آخر بيانات ناجحة محفوظة.';
        } else if (_manualMarketSettings.isComplete) {
          _marketSnapshot = _manualMarketSettings.toSnapshot();
          _marketError = 'تعذّر التحديث عبر الإنترنت، ويجري الآن عرض القيم اليدوية المحفوظة.';
        } else {
          _marketError = 'تعذّر جلب مؤشرات السوق عبر الإنترنت. يمكنك إدخال قيم يدوية من الإعدادات.';
        }
        _marketLoading = false;
        _marketRefreshing = false;
      });
    }
  }


  Future<void> _prepareLocationWeather() async {
    final cached = await _locationWeatherStore.loadCachedSnapshot();
    if (!mounted) return;
    setState(() {
      if (cached != null) {
        _locationWeatherSnapshot = cached;
        _locationWeatherLoading = false;
      }
    });
    await _refreshLocationWeather(initialLoad: cached == null);
  }

  Future<void> _refreshLocationWeather({bool initialLoad = false}) async {
    if (_locationWeatherRefreshing) return;

    if (mounted) {
      setState(() {
        _locationWeatherRefreshing = true;
        if (initialLoad && _locationWeatherSnapshot == null) {
          _locationWeatherLoading = true;
        }
      });
    }

    try {
      final snapshot = await DeviceLocationWeatherService.fetchSnapshot();
      await _locationWeatherStore.saveCachedSnapshot(snapshot);
      if (!mounted) return;
      setState(() {
        _locationWeatherSnapshot = snapshot;
        _locationWeatherError = null;
        _locationWeatherLoading = false;
        _locationWeatherRefreshing = false;
      });
    } catch (e) {
      final cached = await _locationWeatherStore.loadCachedSnapshot();
      if (!mounted) return;
      setState(() {
        if (cached != null) {
          _locationWeatherSnapshot = cached;
          _locationWeatherError =
              'تعذّر تحديث التاريخ والطقس من الإنترنت الآن، ويجري عرض آخر بيانات ناجحة محفوظة.';
        } else {
          _locationWeatherError = e.toString();
        }
        _locationWeatherLoading = false;
        _locationWeatherRefreshing = false;
      });
    }
  }

  DateTime get _locationAwareNow {
    final snapshot = _locationWeatherSnapshot;
    if (snapshot == null) return DateTime.now();
    return DateTime.now().toUtc().add(Duration(seconds: snapshot.utcOffsetSeconds));
  }

  String _timeText(DateTime value) {
    return DateFormat('HH:mm:ss', 'ar').format(value);
  }

  String _fullDateText(DateTime value) {
    return DateFormat('EEEE، d MMMM yyyy', 'ar').format(value);
  }

  String _coordinatesText(LocationWeatherSnapshot snapshot) {
    final lat = snapshot.latitude.toStringAsFixed(3);
    final lon = snapshot.longitude.toStringAsFixed(3);
    return '$lat°, $lon°';
  }

  IconData _weatherIcon(LocationWeatherSnapshot snapshot) {
    final code = snapshot.weatherCode;
    if (code == 0) {
      return snapshot.isDay ? Icons.wb_sunny_rounded : Icons.nightlight_round;
    }
    if (code == 1 || code == 2) return Icons.cloud_queue;
    if (code == 3) return Icons.cloud_outlined;
    if (code == 45 || code == 48) return Icons.blur_on;
    if ([51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82].contains(code)) {
      return Icons.grain;
    }
    if ([71, 73, 75, 77, 85, 86].contains(code)) return Icons.ac_unit;
    if ([95, 96, 99].contains(code)) return Icons.flash_on;
    return Icons.cloud_queue;
  }

  Widget _buildLocationWeatherSection() {
    if (_locationWeatherLoading) {
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(18),
          child: Row(
            children: [
              SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(strokeWidth: 2.5),
              ),
              SizedBox(width: 10),
              Expanded(
                child: Text('جارٍ تجهيز التاريخ والوقت والطقس من الإنترنت مباشرة...'),
              ),
            ],
          ),
        ),
      );
    }

    final snapshot = _locationWeatherSnapshot;
    if (snapshot == null) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(18),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'التاريخ والوقت والطقس',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              Text(
                _locationWeatherError ?? 'تعذّر جلب الوقت أو التاريخ أو الطقس من الإنترنت حاليًا.',
                style: const TextStyle(height: 1.5, color: Colors.black54),
              ),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: _refreshLocationWeather,
                icon: const Icon(Icons.my_location),
                label: const Text('إعادة المحاولة'),
              ),
            ],
          ),
        ),
      );
    }

    final now = _locationAwareNow;
    final weatherColor = snapshot.isDay ? const Color(0xFF1F6FEB) : const Color(0xFF6E56CF);

    Widget infoBox({required IconData icon, required String title, required String value}) {
      return Expanded(
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
          decoration: BoxDecoration(
            color: Colors.grey.shade50,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: Colors.grey.shade200),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(icon, size: 16, color: weatherColor),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Text(
                      title,
                      style: const TextStyle(fontSize: 12.5, color: Colors.black54),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text(
                value,
                style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      );
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 22,
                  backgroundColor: weatherColor.withValues(alpha: 0.12),
                  child: Icon(_weatherIcon(snapshot), color: weatherColor),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'التاريخ والوقت والطقس',
                        style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        '${snapshot.description} • ${snapshot.timezoneAbbreviation}',
                        style: const TextStyle(color: Colors.black54, height: 1.4),
                      ),
                    ],
                  ),
                ),
                IconButton(
                  onPressed: _locationWeatherRefreshing ? null : _refreshLocationWeather,
                  tooltip: 'تحديث الوقت والطقس',
                  icon: _locationWeatherRefreshing
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.refresh),
                ),
              ],
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                infoBox(
                  icon: Icons.today_outlined,
                  title: 'التاريخ',
                  value: _fullDateText(now),
                ),
                const SizedBox(width: 8),
                infoBox(
                  icon: Icons.access_time,
                  title: 'الوقت',
                  value: _timeText(now),
                ),
                const SizedBox(width: 8),
                infoBox(
                  icon: _weatherIcon(snapshot),
                  title: 'الطقس',
                  value: '${snapshot.temperatureC.toStringAsFixed(1)}° • ${snapshot.description}',
                ),
              ],
            ),
            if (_locationWeatherError != null) ...[
              const SizedBox(height: 10),
              Text(
                _locationWeatherError!,
                style: const TextStyle(fontSize: 12.5, color: Colors.black54, height: 1.5),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _loadEntries({bool showStartupMessage = false}) async {
    setState(() => _loading = true);
    try {
      final data = await DatabaseHelper.instance.getAllEntries();
      final backupInfo = await BackupService.instance.readInfo(_userEmail);
      final cloudInfo = await CloudBackupService.instance.readInfo(_userEmail);
      if (!mounted) return;
      setState(() {
        _entries = data;
        _backupInfo = backupInfo;
        _cloudBackupInfo = cloudInfo;
        _loading = false;
      });
      if (showStartupMessage && widget.startupMessage != null) {
        unawaited(
          Future<void>.delayed(const Duration(milliseconds: 300), () {
            if (!mounted) return;
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text(widget.startupMessage!)),
            );
          }),
        );
      }
    } catch (e) {
      if (!mounted) return;
      setState(() => _loading = false);
      _showMessage('تعذّر تحميل السجلات: $e');
    }
  }

  Future<void> _writeBackup({bool showMessage = false}) async {
    if (_cloudSyncing) return;
    try {
      setState(() => _cloudSyncing = true);
      final data = await DatabaseHelper.instance.getAllEntries();
      final localSaved = await BackupService.instance.writeBackup(
        email: _userEmail,
        entries: data,
      );
      final authenticatedEmail = AuthService.instance.currentUserEmail?.trim().toLowerCase();
      final canCloudSync = AuthService.instance.isAvailable && authenticatedEmail == _userEmail.trim().toLowerCase();
      final cloudSaved = canCloudSync
          ? await CloudBackupService.instance.writeBackup(
              email: _userEmail,
              entries: data,
            )
          : false;
      final backupInfo = await BackupService.instance.readInfo(_userEmail);
      final cloudInfo = canCloudSync
          ? await CloudBackupService.instance.readInfo(_userEmail)
          : const CloudBackupSnapshotInfo(exists: false);
      if (!mounted) return;
      setState(() {
        _backupInfo = backupInfo;
        _cloudBackupInfo = cloudInfo;
        _cloudSyncing = false;
      });
      if (showMessage) {
        if (localSaved && cloudSaved) {
          _showMessage('تم تحديث النسخة المحلية والسحابية بنجاح.');
        } else if (localSaved && !canCloudSync) {
          _showMessage('تم تحديث النسخة المحلية. سجّل الدخول من شاشة البداية بنفس الإيميل لتفعيل النسخة السحابية.');
        } else if (localSaved) {
          _showMessage(
            'تم تحديث النسخة المحلية، لكن تعذّرت المزامنة السحابية${CloudBackupService.instance.lastError == null ? '' : ': ${CloudBackupService.instance.lastError}'}',
          );
        } else {
          _showMessage(
            'تعذّر تحديث النسخة المحلية${BackupService.instance.lastError == null ? '' : ': ${BackupService.instance.lastError}'}',
          );
        }
      }
    } catch (e) {
      if (!mounted) return;
      setState(() => _cloudSyncing = false);
      _showMessage('تعذّر إنشاء النسخة الاحتياطية: $e');
    }
  }

  Future<void> _shareBackupToEmail() async {
    if (_sharingBackup) return;
    setState(() => _sharingBackup = true);
    try {
      final data = await DatabaseHelper.instance.getAllEntries();
      final file = await BackupService.instance.exportFile(email: _userEmail, entries: data);
      final backupInfo = await BackupService.instance.readInfo(_userEmail);
      if (!mounted) return;
      setState(() {
        _backupInfo = backupInfo;
      });
      if (file == null) {
        _showMessage('تعذّر تجهيز ملف النسخة الاحتياطية للمشاركة.');
        return;
      }

      await Share.shareXFiles(
        [XFile(file.path)],
        subject: 'نسخة احتياطية - Maen Accountings',
        text: 'هذه نسخة احتياطية مرتبطة بالبريد الشخصي: $_userEmail\n\nاختر تطبيق البريد الإلكتروني وأرسل الملف إلى بريدك الشخصي للاحتفاظ بنسخة خارج الجهاز.',
      );
    } catch (e) {
      if (!mounted) return;
      _showMessage('تعذّرت مشاركة النسخة الاحتياطية: $e');
    } finally {
      if (mounted) setState(() => _sharingBackup = false);
    }
  }

  Future<void> _editManualMarketSettings() async {
    final goldController = TextEditingController(
      text: _manualMarketSettings.gold?.toStringAsFixed(2) ?? '',
    );
    final silverController = TextEditingController(
      text: _manualMarketSettings.silver?.toStringAsFixed(2) ?? '',
    );
    final eurUsdController = TextEditingController(
      text: _manualMarketSettings.eurUsd?.toStringAsFixed(4) ?? '',
    );

    final saved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('إعداد قيم السوق اليدوية'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: goldController,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(
                  labelText: 'الذهب (دولار / أونصة)',
                  prefixIcon: Icon(Icons.workspace_premium_outlined),
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: silverController,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(
                  labelText: 'الفضة (دولار / أونصة)',
                  prefixIcon: Icon(Icons.brightness_5_outlined),
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: eurUsdController,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(
                  labelText: 'اليورو مقابل الدولار',
                  prefixIcon: Icon(Icons.currency_exchange),
                ),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('حفظ'),
          ),
        ],
      ),
    );

    if (saved != true) return;

    double? parseManual(String value) {
      final cleaned = value.trim();
      if (cleaned.isEmpty) return null;
      return double.tryParse(cleaned.replaceAll(',', '.'));
    }

    final manual = ManualMarketSettings(
      gold: parseManual(goldController.text),
      silver: parseManual(silverController.text),
      eurUsd: parseManual(eurUsdController.text),
    );

    await _marketSettingsStore.saveManual(manual);
    if (!mounted) return;
    setState(() {
      _manualMarketSettings = manual;
      if ((_marketSnapshot == null || _marketError != null) && manual.isComplete) {
        _marketSnapshot = manual.toSnapshot();
      }
    });
    _showMessage('تم حفظ القيم اليدوية لمؤشرات السوق.');
    unawaited(_refreshMarketData());
  }

  Future<void> _confirmSignOut() async {
    final approved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('تسجيل الخروج'),
        content: const Text(
          'سيبقى كل شيء محفوظًا داخل الجهاز، كما ستبقى آخر نسخة محلية وسحابية محفوظة. هل تريد تسجيل الخروج الآن؟',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('تسجيل الخروج'),
          ),
        ],
      ),
    );

    if (approved != true) return;
    await widget.onSignOut();
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  double _parseNumber(String value) {
    return double.tryParse(value.trim().replaceAll(',', '.')) ?? 0;
  }

  String _currency(double value) {
    final formatter = NumberFormat('#,##0.00', 'ar');
    return formatter.format(value);
  }

  String _dateText(DateTime value) {
    return DateFormat('yyyy-MM-dd', 'ar').format(value);
  }

  String _dateTimeText(DateTime value) {
    return DateFormat('yyyy-MM-dd – HH:mm', 'ar').format(value);
  }


  String _marketValue(MarketQuote quote) {
    final pattern = quote.symbol == 'EUR/USD' ? '#,##0.0000' : '#,##0.00';
    return NumberFormat(pattern, 'en').format(quote.value);
  }

  String _monthText(DateTime value) {
    return DateFormat('MMMM yyyy', 'ar').format(value);
  }

  List<ProfitEntry> get _currentMonthEntries {
    final now = DateTime.now();
    return _entries.where((entry) {
      return entry.date.year == now.year && entry.date.month == now.month;
    }).toList();
  }

  List<ProfitEntry> get _currentYearEntries {
    final now = DateTime.now();
    return _entries.where((entry) => entry.date.year == now.year).toList();
  }

  List<ProfitEntry> get _monthlyEntries {
    return _entries.where((entry) {
      return entry.date.year == _reportDate.year && entry.date.month == _reportDate.month;
    }).toList();
  }

  List<ProfitEntry> get _yearlyEntries {
    return _entries.where((entry) => entry.date.year == _reportDate.year).toList();
  }

  List<ProfitEntry> get _filteredEntries {
    final query = _searchController.text.trim().toLowerCase();
    if (query.isEmpty) return _entries;
    return _entries.where((entry) {
      return _dateText(entry.date).toLowerCase().contains(query) ||
          entry.notes.toLowerCase().contains(query);
    }).toList();
  }

  ProfitEntry? get _todayEntry {
    final today = DateTime.now();
    for (final entry in _entries) {
      if (entry.date.year == today.year &&
          entry.date.month == today.month &&
          entry.date.day == today.day) {
        return entry;
      }
    }
    return null;
  }

  double _salesTotal(List<ProfitEntry> list) {
    return list.fold(0, (sum, item) => sum + item.sales);
  }

  double _grossTotal(List<ProfitEntry> list) {
    return list.fold(0, (sum, item) => sum + item.grossProfit);
  }

  double _expensesTotal(List<ProfitEntry> list) {
    return list.fold(0, (sum, item) => sum + item.expenses);
  }

  double _netTotal(List<ProfitEntry> list) {
    return list.fold(0, (sum, item) => sum + item.netProfit);
  }

  double _averageNet(List<ProfitEntry> list) {
    if (list.isEmpty) return 0;
    return _netTotal(list) / list.length;
  }

  double get _previewGross {
    return _parseNumber(_salesController.text) - _parseNumber(_costController.text);
  }

  double get _previewNet {
    return _previewGross - _parseNumber(_expensesController.text);
  }

  Future<void> _pickEntryDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _selectedDate,
      firstDate: DateTime(2020),
      lastDate: DateTime(2100),
      locale: const Locale('ar'),
      helpText: 'اختر تاريخ السجل',
    );
    if (picked == null) return;
    setState(() {
      _selectedDate = DateTime(picked.year, picked.month, picked.day);
    });
  }

  Future<void> _pickReportMonth() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _reportDate,
      firstDate: DateTime(2020),
      lastDate: DateTime(2100),
      locale: const Locale('ar'),
      helpText: 'اختر أي يوم من الشهر المطلوب',
    );
    if (picked == null) return;
    setState(() {
      _reportDate = DateTime(picked.year, picked.month, 1);
    });
  }

  void _clearForm() {
    _formKey.currentState?.reset();
    _salesController.clear();
    _costController.clear();
    _expensesController.text = '0';
    _notesController.clear();
    _editingEntry = null;
    _selectedDate = DateTime.now();
    setState(() {});
  }

  Future<void> _saveEntry() async {
    if (!_formKey.currentState!.validate()) return;

    final wasEditing = _editingEntry != null;
    final entry = ProfitEntry(
      id: _editingEntry?.id,
      date: DateTime(_selectedDate.year, _selectedDate.month, _selectedDate.day),
      sales: _parseNumber(_salesController.text),
      cost: _parseNumber(_costController.text),
      expenses: _parseNumber(_expensesController.text),
      notes: _notesController.text.trim(),
    );

    setState(() => _saving = true);

    try {
      await DatabaseHelper.instance.insertOrReplaceEntry(entry);
      await _loadEntries();
      await _writeBackup();

      if (!mounted) return;
      setState(() {
        _saving = false;
        _selectedIndex = 0;
      });
      _clearForm();
      _showMessage(
        wasEditing
            ? 'تم تحديث السجل وتحديث النسخة المحلية والسحابية.'
            : 'تم حفظ السجل وتحديث النسخة المحلية والسحابية.',
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _saving = false);
      _showMessage('تعذّر حفظ السجل: $e');
    }
  }

  void _editEntry(ProfitEntry entry) {
    _editingEntry = entry;
    _selectedDate = entry.date;
    _salesController.text = entry.sales.toStringAsFixed(2);
    _costController.text = entry.cost.toStringAsFixed(2);
    _expensesController.text = entry.expenses.toStringAsFixed(2);
    _notesController.text = entry.notes;
    setState(() => _selectedIndex = 1);
  }

  Future<void> _confirmDelete(ProfitEntry entry) async {
    final approved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('حذف السجل'),
        content: Text('هل تريد حذف سجل يوم ${_dateText(entry.date)}؟'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('حذف'),
          ),
        ],
      ),
    );

    if (approved != true || entry.id == null) return;
    try {
      await DatabaseHelper.instance.deleteEntry(entry.id!);
      await _loadEntries();
      await _writeBackup();
      if (!mounted) return;
      _showMessage('تم حذف السجل وتحديث النسخة المحلية والسحابية.');
    } catch (e) {
      if (!mounted) return;
      _showMessage('تعذّر حذف السجل: $e');
    }
  }

  Widget _sectionHeader(String title, {String? subtitle, Widget? trailing}) {
    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
              ),
              if (subtitle != null) ...[
                const SizedBox(height: 4),
                Text(subtitle, style: const TextStyle(color: Colors.black54, height: 1.4)),
              ],
            ],
          ),
        ),
        if (trailing != null) trailing,
      ],
    );
  }

  Widget _statCard({
    required String title,
    required String value,
    required IconData icon,
    Color? color,
    String? footer,
  }) {
    final baseColor = color ?? Theme.of(context).colorScheme.primary;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            CircleAvatar(
              radius: 22,
              backgroundColor: baseColor.withValues(alpha: 0.12),
              child: Icon(icon, color: baseColor),
            ),
            const SizedBox(height: 14),
            Text(title, style: const TextStyle(fontSize: 13, color: Colors.black54)),
            const SizedBox(height: 6),
            Text(
              value,
              style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
            ),
            if (footer != null) ...[
              const SizedBox(height: 8),
              Text(footer, style: const TextStyle(fontSize: 12.5, color: Colors.black54)),
            ],
          ],
        ),
      ),
    );
  }

  Widget _statsWrap(List<Widget> cards) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final maxWidth = constraints.maxWidth;
        final crossAxisCount = maxWidth > 920 ? 4 : maxWidth > 640 ? 2 : 1;
        final spacing = 12.0;
        final itemWidth = (maxWidth - spacing * (crossAxisCount - 1)) / crossAxisCount;

        return Wrap(
          spacing: spacing,
          runSpacing: spacing,
          children: cards
              .map((card) => SizedBox(width: itemWidth, child: card))
              .toList(growable: false),
        );
      },
    );
  }

  Widget _entryTile(ProfitEntry entry, {bool compact = false}) {
    return Card(
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
        leading: CircleAvatar(
          backgroundColor: (entry.netProfit >= 0 ? Colors.green : Colors.red).withValues(alpha: 0.12),
          child: Icon(
            entry.netProfit >= 0 ? Icons.trending_up : Icons.trending_down,
            color: entry.netProfit >= 0 ? Colors.green : Colors.red,
          ),
        ),
        title: Text(
          _dateText(entry.date),
          style: const TextStyle(fontWeight: FontWeight.bold),
        ),
        subtitle: Padding(
          padding: const EdgeInsets.only(top: 8),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('المبيعات: ${_currency(entry.sales)}'),
              Text('المشتريات (تكلفة البضاعة): ${_currency(entry.cost)}'),
              Text('المصاريف التشغيلية: ${_currency(entry.expenses)}'),
              Text('الربح قبل المصاريف التشغيلية: ${_currency(entry.grossProfit)}'),
              Text(
                'صافي الربح بعد المصاريف: ${_currency(entry.netProfit)}',
                style: TextStyle(
                  fontWeight: FontWeight.bold,
                  color: entry.netProfit >= 0 ? Colors.green : Colors.red,
                ),
              ),
              if (!compact && entry.notes.trim().isNotEmpty) ...[
                const SizedBox(height: 4),
                Text('ملاحظات: ${entry.notes}'),
              ],
            ],
          ),
        ),
        trailing: PopupMenuButton<String>(
          onSelected: (value) {
            if (value == 'edit') {
              _editEntry(entry);
            } else if (value == 'delete') {
              _confirmDelete(entry);
            }
          },
          itemBuilder: (context) => const [
            PopupMenuItem(value: 'edit', child: Text('تعديل')),
            PopupMenuItem(value: 'delete', child: Text('حذف')),
          ],
        ),
      ),
    );
  }


  Widget _outlineActionButton({
    required IconData icon,
    required String label,
    VoidCallback? onPressed,
  }) {
    return OutlinedButton.icon(
      onPressed: onPressed,
      icon: Icon(icon),
      label: Text(label),
    );
  }

  String _marketShortTitle(MarketQuote quote) {
    if (quote.symbol.contains('XAU')) return 'ذهب';
    if (quote.symbol.contains('XAG')) return 'فضة';
    if (quote.symbol.contains('EUR')) return 'EUR/USD';
    return quote.title;
  }

  Widget _marketTile(MarketQuote quote) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 18,
                  backgroundColor: quote.color.withValues(alpha: 0.12),
                  child: Icon(quote.icon, color: quote.color, size: 18),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    _marketShortTitle(quote),
                    style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13.5),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              _marketValue(quote),
              style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
            const SizedBox(height: 4),
            Text(
              quote.unit,
              style: const TextStyle(fontSize: 11.5, color: Colors.black54),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildMarketSection() {
    if (_marketLoading) {
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(18),
          child: Row(
            children: [
              SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(strokeWidth: 2.5),
              ),
              SizedBox(width: 10),
              Expanded(
                child: Text('جارٍ جلب أسعار الذهب والفضة واليورو/الدولار...'),
              ),
            ],
          ),
        ),
      );
    }

    if (_marketError != null && _marketSnapshot == null) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(18),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'تعذّر تحميل مؤشرات السوق',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              Text(
                _marketError!,
                style: const TextStyle(height: 1.5, color: Colors.black54),
              ),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: _refreshMarketData,
                icon: const Icon(Icons.refresh),
                label: const Text('إعادة المحاولة'),
              ),
            ],
          ),
        ),
      );
    }

    final snapshot = _marketSnapshot;
    if (snapshot == null) {
      return const SizedBox.shrink();
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Expanded(
                  child: Text(
                    'الذهب • الفضة • EUR/USD',
                    style: TextStyle(fontSize: 17, fontWeight: FontWeight.bold),
                  ),
                ),
                IconButton(
                  onPressed: _marketRefreshing ? null : _refreshMarketData,
                  tooltip: 'تحديث مؤشرات السوق',
                  icon: _marketRefreshing
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.wifi_tethering_outlined),
                ),
              ],
            ),
            Text(
              _marketError == null
                  ? 'آخر مزامنة: ${_dateTimeText(snapshot.fetchedAt)}'
                  : 'آخر بيانات محفوظة: ${_dateTimeText(snapshot.fetchedAt)}',
              style: const TextStyle(fontSize: 12.5, color: Colors.black54),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                for (int i = 0; i < snapshot.quotes.length; i++) ...[
                  Expanded(child: _marketTile(snapshot.quotes[i])),
                  if (i != snapshot.quotes.length - 1) const SizedBox(width: 8),
                ],
              ],
            ),
            if (_marketError != null) ...[
              const SizedBox(height: 10),
              Text(
                _marketError!,
                style: const TextStyle(fontSize: 12.5, color: Colors.black54, height: 1.5),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildDashboardPage() {
    final today = _todayEntry;
    final latestEntries = _entries.take(5).toList();
    final monthNet = _netTotal(_currentMonthEntries);
    final monthSales = _salesTotal(_currentMonthEntries);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _buildLocationWeatherSection(),
        const SizedBox(height: 14),
        _buildMarketSection(),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  spacing: 14,
                  runSpacing: 14,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    CircleAvatar(
                      radius: 30,
                      backgroundColor: Theme.of(context).colorScheme.primary.withValues(alpha: 0.12),
                      child: Icon(
                        Icons.account_balance_wallet_outlined,
                        size: 30,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                    ConstrainedBox(
                      constraints: const BoxConstraints(minWidth: 220, maxWidth: 520),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            'لوحة التحكم الرئيسية',
                            style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            'البريد الشخصي المرتبط: $_userEmail',
                            style: const TextStyle(color: Colors.black54),
                          ),
                          const SizedBox(height: 10),
                          Text(
                            today == null
                                ? 'لا يوجد سجل محفوظ لليوم بعد. ابدأ بإضافة سجل اليوم ليظهر ملخص الربح مباشرة.'
                                : 'صافي ربح اليوم: ${_currency(today.netProfit)} • مبيعات اليوم: ${_currency(today.sales)}',
                            style: const TextStyle(fontSize: 16.5, fontWeight: FontWeight.w700, height: 1.5),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 18),
                Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: [
                    _outlineActionButton(
                      icon: Icons.add_chart_outlined,
                      label: 'إضافة سجل',
                      onPressed: () => setState(() => _selectedIndex = 1),
                    ),
                    _outlineActionButton(
                      icon: Icons.analytics_outlined,
                      label: 'التقارير',
                      onPressed: () => setState(() => _selectedIndex = 2),
                    ),
                    _outlineActionButton(
                      icon: Icons.backup_outlined,
                      label: 'مزامنة الآن',
                      onPressed: () => _writeBackup(showMessage: true),
                    ),
                    _outlineActionButton(
                      icon: Icons.forward_to_inbox_outlined,
                      label: 'إرسال إلى البريد',
                      onPressed: _sharingBackup ? null : _shareBackupToEmail,
                    ),
                    _outlineActionButton(
                      icon: Icons.tune,
                      label: 'قيم السوق اليدوية',
                      onPressed: _editManualMarketSettings,
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        _statsWrap([
          _statCard(
            title: 'صافي هذا الشهر',
            value: _currency(monthNet),
            icon: Icons.calendar_month_outlined,
            color: Colors.green,
            footer: 'عدد الأيام المسجلة: ${_currentMonthEntries.length}',
          ),
          _statCard(
            title: 'مبيعات هذا الشهر',
            value: _currency(monthSales),
            icon: Icons.sell_outlined,
            color: Colors.blue,
            footer: 'صافي اليوم الحالي: ${today == null ? '—' : _currency(today.netProfit)}',
          ),
          _statCard(
            title: 'صافي هذه السنة',
            value: _currency(_netTotal(_currentYearEntries)),
            icon: Icons.bar_chart_outlined,
            color: Colors.indigo,
            footer: 'متوسط السجل: ${_currency(_averageNet(_currentYearEntries))}',
          ),
          _statCard(
            title: 'إجمالي المصاريف التشغيلية',
            value: _currency(_expensesTotal(_entries)),
            icon: Icons.payments_outlined,
            color: Colors.orange,
            footer: 'إجمالي السجلات: ${_entries.length}',
          ),
        ]),
        const SizedBox(height: 20),
        _sectionHeader(
          'أحدث السجلات',
          subtitle: 'آخر 5 سجلات تم إدخالها',
          trailing: TextButton(
            onPressed: () => setState(() => _selectedIndex = 2),
            child: const Text('عرض الكل'),
          ),
        ),
        const SizedBox(height: 10),
        if (latestEntries.isEmpty)
          const _EmptyStateCard(
            icon: Icons.inbox_outlined,
            title: 'لا توجد سجلات بعد',
            subtitle: 'ابدأ بإضافة أول سجل يومي ليظهر هنا الملخص الكامل.',
          )
        else
          ...latestEntries.map((entry) => _entryTile(entry, compact: true)),
      ],
    );
  }

  Widget _buildEntryPage() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _sectionHeader(
          _editingEntry == null ? 'إضافة سجل يومي' : 'تعديل سجل يومي',
          subtitle: 'أدخل بيانات اليوم وسيتم تحديث التقارير والنسخة المحلية تلقائيًا.',
        ),
        const SizedBox(height: 12),
        Card(
          color: Colors.blueGrey.withValues(alpha: 0.05),
          child: const Padding(
            padding: EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(Icons.info_outline),
                    SizedBox(width: 8),
                    Text(
                      'توضيح طريقة الحساب',
                      style: TextStyle(fontWeight: FontWeight.bold),
                    ),
                  ],
                ),
                SizedBox(height: 10),
                Text('1) المبيعات اليومية: مجموع ما تم بيعه خلال اليوم.'),
                SizedBox(height: 4),
                Text('2) المشتريات / تكلفة البضاعة: تكلفة الأصناف التي تم بيعها.'),
                SizedBox(height: 4),
                Text('3) المصاريف التشغيلية: مثل النقل والإيجار والعمالة والمصاريف اليومية الأخرى.'),
                SizedBox(height: 8),
                Text(
                  'المعادلة: صافي الربح = المبيعات - المشتريات - المصاريف التشغيلية',
                  style: TextStyle(fontWeight: FontWeight.w600),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  OutlinedButton.icon(
                    onPressed: _pickEntryDate,
                    icon: const Icon(Icons.calendar_month),
                    label: Text('التاريخ: ${_dateText(_selectedDate)}'),
                  ),
                  const SizedBox(height: 14),
                  TextFormField(
                    controller: _salesController,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
                    decoration: const InputDecoration(
                      labelText: 'إجمالي المبيعات اليومية',
                      prefixIcon: Icon(Icons.sell_outlined),
                    ),
                    onChanged: (_) => setState(() {}),
                    validator: (value) {
                      final number = _parseNumber(value ?? '');
                      if (number <= 0) return 'أدخل مبلغ بيع صحيح';
                      return null;
                    },
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _costController,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
                    decoration: const InputDecoration(
                      labelText: 'إجمالي المشتريات اليومية (تكلفة البضاعة)',
                      prefixIcon: Icon(Icons.inventory_2_outlined),
                    ),
                    onChanged: (_) => setState(() {}),
                    validator: (value) {
                      final number = _parseNumber(value ?? '');
                      if (number < 0) return 'أدخل قيمة مشتريات صحيحة';
                      return null;
                    },
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _expensesController,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
                    decoration: const InputDecoration(
                      labelText: 'المصاريف التشغيلية اليومية',
                      prefixIcon: Icon(Icons.money_off_csred_outlined),
                    ),
                    onChanged: (_) => setState(() {}),
                    validator: (value) {
                      final number = _parseNumber(value ?? '');
                      if (number < 0) return 'أدخل مصاريف تشغيلية صحيحة';
                      return null;
                    },
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _notesController,
                    minLines: 2,
                    maxLines: 4,
                    decoration: const InputDecoration(
                      labelText: 'ملاحظات اختيارية',
                      prefixIcon: Icon(Icons.notes_outlined),
                      alignLabelWithHint: true,
                    ),
                  ),
                  const SizedBox(height: 16),
                  _statsWrap([
                    _statCard(
                      title: 'الربح قبل المصاريف التشغيلية',
                      value: _currency(_previewGross),
                      icon: Icons.trending_up,
                      color: Colors.blue,
                    ),
                    _statCard(
                      title: 'صافي الربح المتوقع',
                      value: _currency(_previewNet),
                      icon: Icons.account_balance_wallet_outlined,
                      color: _previewNet >= 0 ? Colors.green : Colors.red,
                    ),
                  ]),
                  const SizedBox(height: 16),
                  Wrap(
                    spacing: 10,
                    runSpacing: 10,
                    children: [
                      SizedBox(
                        width: 220,
                        child: FilledButton.icon(
                          onPressed: _saving ? null : _saveEntry,
                          icon: _saving
                              ? const SizedBox(
                                  width: 16,
                                  height: 16,
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : Icon(_editingEntry == null ? Icons.save : Icons.check_circle),
                          label: Text(_editingEntry == null ? 'حفظ السجل' : 'تحديث السجل'),
                        ),
                      ),
                      SizedBox(
                        width: 220,
                        child: OutlinedButton.icon(
                          onPressed: _clearForm,
                          icon: const Icon(Icons.refresh),
                          label: const Text('تفريغ الحقول'),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'ملاحظة: التطبيق يعتمد سجلًا واحدًا لكل يوم. عند حفظ نفس التاريخ مرة أخرى سيتم استبدال السجل القديم.',
                    style: TextStyle(fontSize: 12.5, color: Colors.black54),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildReportsPage() {
    final monthlyEntries = _monthlyEntries;
    final yearlyEntries = _yearlyEntries;
    final filteredEntries = _filteredEntries;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _sectionHeader(
          'التقارير والسجلات',
          subtitle: 'واجهة موحّدة لمتابعة الأداء الشهري والسنوي ومراجعة كل السجلات من مكان واحد.',
          trailing: OutlinedButton.icon(
            onPressed: _pickReportMonth,
            icon: const Icon(Icons.date_range),
            label: Text(_monthText(_reportDate)),
          ),
        ),
        const SizedBox(height: 12),
        _statsWrap([
          _statCard(
            title: 'صافي الشهر المحدد',
            value: _currency(_netTotal(monthlyEntries)),
            icon: Icons.calendar_view_month,
            color: Colors.teal,
            footer: 'عدد الأيام المسجلة: ${monthlyEntries.length}',
          ),
          _statCard(
            title: 'إجمالي مصاريف الشهر',
            value: _currency(_expensesTotal(monthlyEntries)),
            icon: Icons.money_off,
            color: Colors.orange,
            footer: 'الربح قبل المصاريف التشغيلية: ${_currency(_grossTotal(monthlyEntries))}',
          ),
          _statCard(
            title: 'صافي سنة ${_reportDate.year}',
            value: _currency(_netTotal(yearlyEntries)),
            icon: Icons.query_stats,
            color: Colors.indigo,
            footer: 'متوسط اليوم: ${_currency(_averageNet(yearlyEntries))}',
          ),
          _statCard(
            title: 'إجمالي السجلات المعروضة',
            value: '${filteredEntries.length}',
            icon: Icons.receipt_long_outlined,
            color: Colors.blueGrey,
            footer: 'إجمالي كل السجلات: ${_entries.length}',
          ),
        ]),
        const SizedBox(height: 18),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'البحث داخل السجلات',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _searchController,
                  decoration: InputDecoration(
                    hintText: 'ابحث بالتاريخ أو بالملاحظات',
                    prefixIcon: const Icon(Icons.search),
                    suffixIcon: _searchController.text.isEmpty
                        ? null
                        : IconButton(
                            onPressed: () => _searchController.clear(),
                            icon: const Icon(Icons.close),
                          ),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        if (filteredEntries.isEmpty)
          const _EmptyStateCard(
            icon: Icons.search_off,
            title: 'لا توجد نتائج',
            subtitle: 'جرّب تغيير كلمات البحث أو أضف سجلات جديدة.',
          )
        else
          ...filteredEntries.map((entry) => _entryTile(entry)),
      ],
    );
  }

  Widget _infoRow(String title, String value, {Color? valueColor}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            flex: 3,
            child: Text(
              title,
              style: const TextStyle(color: Colors.black54, fontWeight: FontWeight.w600),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            flex: 5,
            child: Text(
              value,
              style: TextStyle(color: valueColor, height: 1.45),
            ),
          ),
        ],
      ),
    );
  }

  Widget _launchCheckItem(String text, bool done) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            done ? Icons.check_circle : Icons.radio_button_unchecked,
            color: done ? Colors.green : Colors.orange,
            size: 18,
          ),
          const SizedBox(width: 8),
          Expanded(child: Text(text, style: const TextStyle(height: 1.45))),
        ],
      ),
    );
  }

  Widget _buildSettingsPage() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _sectionHeader(
          'الإعدادات والجاهزية',
          subtitle: 'إدارة البريد الشخصي والنسخة الاحتياطية ومراجعة جاهزية التطبيق للاستخدام اليومي.',
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'الحساب والنسخة الاحتياطية',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 14),
                _infoRow('الحساب الحالي', _userEmail),
                _infoRow(
                  'النسخة المحلية',
                  _backupInfo.exists
                      ? 'متوفرة (${_backupInfo.entriesCount} سجل)'
                      : 'غير موجودة بعد',
                  valueColor: _backupInfo.exists ? Colors.green.shade700 : Colors.orange.shade900,
                ),
                _infoRow(
                  'آخر تحديث محلي',
                  _backupInfo.updatedAt == null ? '—' : _dateTimeText(_backupInfo.updatedAt!),
                ),
                _infoRow(
                  'النسخة السحابية',
                  _cloudBackupInfo.exists
                      ? 'متوفرة (${_cloudBackupInfo.entriesCount} سجل)'
                      : 'غير متوفرة بعد',
                  valueColor: _cloudBackupInfo.exists ? Colors.green.shade700 : Colors.orange.shade900,
                ),
                _infoRow(
                  'آخر تحديث سحابي',
                  _cloudBackupInfo.updatedAt == null ? '—' : _dateTimeText(_cloudBackupInfo.updatedAt!),
                ),
                _infoRow(
                  'مسار الملف المحلي',
                  _backupInfo.filePath ?? 'سيظهر بعد إنشاء أول نسخة احتياطية',
                ),
                _infoRow(
                  'آخر تحديث للطقس',
                  _locationWeatherSnapshot == null
                      ? '—'
                      : _dateTimeText(_locationWeatherSnapshot!.fetchedAt),
                ),
                _infoRow(
                  'المنطقة الزمنية الحالية',
                  _locationWeatherSnapshot == null
                      ? '—'
                      : '${_locationWeatherSnapshot!.timezone} (${_locationWeatherSnapshot!.timezoneAbbreviation})',
                ),
                _infoRow(
                  'الإحداثيات التقريبية',
                  _locationWeatherSnapshot == null
                      ? '—'
                      : _coordinatesText(_locationWeatherSnapshot!),
                ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: [
                    FilledButton.icon(
                      onPressed: _cloudSyncing ? null : () => _writeBackup(showMessage: true),
                      icon: _cloudSyncing
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.cloud_upload_outlined),
                      label: const Text('مزامنة محلية وسحابية الآن'),
                    ),
                    OutlinedButton.icon(
                      onPressed: _sharingBackup ? null : _shareBackupToEmail,
                      icon: _sharingBackup
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.forward_to_inbox_outlined),
                      label: const Text('إرسال نسخة إلى البريد'),
                    ),
                    OutlinedButton.icon(
                      onPressed: _locationWeatherRefreshing ? null : _refreshLocationWeather,
                      icon: _locationWeatherRefreshing
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.my_location),
                      label: const Text('تحديث الوقت والطقس'),
                    ),
                    OutlinedButton.icon(
                      onPressed: _editManualMarketSettings,
                      icon: const Icon(Icons.tune),
                      label: const Text('قيم السوق اليدوية'),
                    ),
                    OutlinedButton.icon(
                      onPressed: _confirmSignOut,
                      icon: const Icon(Icons.logout),
                      label: const Text('تسجيل الخروج'),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'مصادر السوق والبدائل',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 14),
                _infoRow(
                  'الوضع الحالي',
                  _marketError == null
                      ? 'الأسعار المباشرة تعمل عبر الإنترنت'
                      : 'يوجد تعذر في الاتصال ويجري استخدام بيانات احتياطية عند توفرها',
                  valueColor: _marketError == null ? Colors.green.shade700 : Colors.orange.shade900,
                ),
                _infoRow(
                  'القيم اليدوية',
                  _manualMarketSettings.isComplete
                      ? 'مكتملة'
                      : (_manualMarketSettings.hasAny ? 'موجودة جزئيًا' : 'غير مضبوطة'),
                ),
                const SizedBox(height: 10),
                const Text(
                  'إذا تعذّر جلب سعر الذهب أو الفضة أو اليورو/الدولار من الإنترنت، يستطيع التطبيق عرض آخر بيانات ناجحة محفوظة أو القيم اليدوية التي تدخلها هنا.',
                  style: TextStyle(height: 1.6),
                ),
                const SizedBox(height: 12),
                Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: [
                    FilledButton.icon(
                      onPressed: _editManualMarketSettings,
                      icon: const Icon(Icons.tune),
                      label: const Text('تعديل القيم اليدوية'),
                    ),
                    OutlinedButton.icon(
                      onPressed: _marketRefreshing ? null : _refreshMarketData,
                      icon: const Icon(Icons.wifi_tethering_outlined),
                      label: const Text('إعادة جلب الأسعار'),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'مؤشرات الجاهزية للإطلاق',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 14),
                _launchCheckItem('واجهة موحّدة ومنظمة مع تنقل واضح بين الرئيسية والإدخال والتقارير والإعدادات.', true),
                _launchCheckItem('هوية المستخدم محفوظة قبل فتح البرنامج.', _userEmail.trim().isNotEmpty),
                _launchCheckItem('كل السجلات تحفظ داخل الجهاز في قاعدة بيانات SQLite.', true),
                _launchCheckItem('يوجد ملف نسخة احتياطية محلي مرتبط بالحساب الحالي.', _backupInfo.exists),
                _launchCheckItem('توجد نسخة سحابية يمكن استعادتها على جهاز آخر بعد تسجيل الدخول.', _cloudBackupInfo.exists),
                _launchCheckItem('يمكن إرسال نسخة إضافية إلى البريد الشخصي عبر تطبيق البريد في الجهاز.', true),
                _launchCheckItem('تقارير شهرية وسنوية وسجل كامل مع بحث وتعديل وحذف.', true),
                _launchCheckItem('يوجد على الأقل سجل واحد صالح داخل البرنامج.', _entries.isNotEmpty),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'ملاحظة تشغيل مهمة',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 10),
                const Text(
                  'حفظ البيانات داخل الجهاز يعمل تلقائيًا، كما تتم مزامنتها تلقائيًا على السحابة بعد تسجيل الدخول. ويمكنك أيضًا إرسال نسخة إضافية إلى بريدك الشخصي يدويًا كإجراء احتياطي خارجي.',
                  style: TextStyle(height: 1.6),
                ),
                const SizedBox(height: 14),
                OutlinedButton.icon(
                  onPressed: _confirmSignOut,
                  icon: const Icon(Icons.logout),
                  label: const Text('تسجيل الخروج'),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    switch (_selectedIndex) {
      case 0:
        return _buildDashboardPage();
      case 1:
        return _buildEntryPage();
      case 2:
        return _buildReportsPage();
      case 3:
        return _buildSettingsPage();
      default:
        return _buildDashboardPage();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Maen Accountings'),
        actions: [
          IconButton(
            tooltip: 'نسخة احتياطية الآن',
            onPressed: () => _writeBackup(showMessage: true),
            icon: const Icon(Icons.backup_outlined),
          ),
          IconButton(
            tooltip: 'تحديث مؤشرات السوق',
            onPressed: _marketRefreshing ? null : _refreshMarketData,
            icon: _marketRefreshing
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.wifi_tethering_outlined),
          ),
          IconButton(
            tooltip: 'مشاركة النسخة إلى البريد',
            onPressed: _sharingBackup ? null : _shareBackupToEmail,
            icon: _sharingBackup
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.forward_to_inbox_outlined),
          ),
        ],
      ),
      body: AnimatedSwitcher(
        duration: const Duration(milliseconds: 220),
        child: KeyedSubtree(
          key: ValueKey(_selectedIndex),
          child: _buildBody(),
        ),
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _selectedIndex,
        onDestinationSelected: (index) => setState(() => _selectedIndex = index),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.space_dashboard_outlined),
            selectedIcon: Icon(Icons.space_dashboard),
            label: 'الرئيسية',
          ),
          NavigationDestination(
            icon: Icon(Icons.edit_note_outlined),
            selectedIcon: Icon(Icons.edit_note),
            label: 'الإدخال',
          ),
          NavigationDestination(
            icon: Icon(Icons.analytics_outlined),
            selectedIcon: Icon(Icons.analytics),
            label: 'التقارير',
          ),
          NavigationDestination(
            icon: Icon(Icons.settings_outlined),
            selectedIcon: Icon(Icons.settings),
            label: 'الإعدادات',
          ),
        ],
      ),
    );
  }
}

class _EmptyStateCard extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;

  const _EmptyStateCard({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          children: [
            CircleAvatar(
              radius: 26,
              backgroundColor: scheme.primary.withValues(alpha: 0.12),
              child: Icon(icon, color: scheme.primary, size: 28),
            ),
            const SizedBox(height: 14),
            Text(
              title,
              style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 8),
            Text(
              subtitle,
              style: const TextStyle(height: 1.5, color: Colors.black54),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}
