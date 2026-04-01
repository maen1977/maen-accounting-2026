import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:flutter/services.dart';
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
      title: 'برنامج تتبع الأرباح',
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
      home: const Directionality(
        textDirection: TextDirection.rtl,
        child: AppStartupGate(),
      ),
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
        .get(Uri.parse('https://api.gold-api.com/price/$symbol'))
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
          .get(Uri.parse('https://api.frankfurter.dev/v2/rate/EUR/USD'))
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
        .get(Uri.parse('https://api.frankfurter.dev/v1/latest?base=EUR&symbols=USD'))
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

class AppStartupGate extends StatefulWidget {
  const AppStartupGate({super.key});

  @override
  State<AppStartupGate> createState() => _AppStartupGateState();
}

class _AppStartupGateState extends State<AppStartupGate> {
  final ProfileStore _profileStore = ProfileStore();
  bool _loading = true;
  String? _userEmail;
  String? _startupMessage;

  @override
  void initState() {
    super.initState();
    _initialize();
  }

  Future<void> _initialize() async {
    final profile = await _profileStore.load();
    String? message;

    if (profile != null) {
      final restored = await BackupService.instance.restoreIfDatabaseEmpty(profile.email);
      if (restored) {
        message = 'تمت استعادة النسخة الاحتياطية المحلية تلقائيًا.';
      }
    }

    if (!mounted) return;
    setState(() {
      _userEmail = profile?.email;
      _startupMessage = message;
      _loading = false;
    });
  }

  Future<void> _completeSetup(String email) async {
    final normalized = email.trim().toLowerCase();
    await _profileStore.saveEmail(normalized);
    final restored = await BackupService.instance.restoreIfDatabaseEmpty(normalized);
    final entries = await DatabaseHelper.instance.getAllEntries();
    await BackupService.instance.writeBackup(email: normalized, entries: entries);

    if (!mounted) return;
    setState(() {
      _userEmail = normalized;
      _startupMessage = restored
          ? 'تم حفظ البريد الشخصي واستعادة النسخة الاحتياطية المحلية.'
          : 'تم حفظ البريد الشخصي وتجهيز النسخة المحلية على الجهاز.';
    });
  }

  Future<void> _updateEmail(String newEmail) async {
    final normalized = newEmail.trim().toLowerCase();
    await _profileStore.saveEmail(normalized);
    final entries = await DatabaseHelper.instance.getAllEntries();
    await BackupService.instance.writeBackup(email: normalized, entries: entries);

    if (!mounted) return;
    setState(() {
      _userEmail = normalized;
      _startupMessage = 'تم تحديث البريد الشخصي وربط النسخة الاحتياطية به.';
    });
  }

  Future<void> _resetProfile() async {
    await _profileStore.clear();
    if (!mounted) return;
    setState(() {
      _userEmail = null;
      _startupMessage = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const SplashLoadingScreen();
    }

    if (_userEmail == null || _userEmail!.isEmpty) {
      return EmailSetupGate(onContinue: _completeSetup);
    }

    return ProfitHomePage(
      userEmail: _userEmail!,
      startupMessage: _startupMessage,
      onEmailUpdated: _updateEmail,
      onResetProfile: _resetProfile,
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
              'جارٍ تجهيز برنامج تتبع الأرباح',
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

class EmailSetupGate extends StatefulWidget {
  final Future<void> Function(String email) onContinue;

  const EmailSetupGate({
    super.key,
    required this.onContinue,
  });

  @override
  State<EmailSetupGate> createState() => _EmailSetupGateState();
}

class _EmailSetupGateState extends State<EmailSetupGate> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _emailController = TextEditingController();
  bool _saving = false;

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  bool _isValidEmail(String value) {
    final emailRegExp = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    return emailRegExp.hasMatch(value.trim());
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    await widget.onContinue(_emailController.text.trim());
    if (!mounted) return;
    setState(() => _saving = false);
  }

  Widget _featureItem(IconData icon, String text) {
    return Row(
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: 8),
        Expanded(child: Text(text, style: const TextStyle(height: 1.4))),
      ],
    );
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
              constraints: const BoxConstraints(maxWidth: 620),
              child: Card(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        CircleAvatar(
                          radius: 28,
                          backgroundColor: scheme.primary.withValues(alpha: 0.12),
                          child: Icon(Icons.mail_lock_outlined, size: 30, color: scheme.primary),
                        ),
                        const SizedBox(height: 16),
                        const Text(
                          'أدخل بريدك الشخصي قبل فتح البرنامج',
                          style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
                        ),
                        const SizedBox(height: 8),
                        const Text(
                          'سيتم ربط البيانات بهذا البريد الشخصي، وحفظ كل السجلات داخل الجهاز في قاعدة محلية وملف احتياطي جاهز للمشاركة إلى بريدك الإلكتروني.',
                          style: TextStyle(height: 1.6),
                        ),
                        const SizedBox(height: 16),
                        Wrap(
                          runSpacing: 10,
                          spacing: 12,
                          children: [
                            SizedBox(
                              width: 260,
                              child: _featureItem(Icons.phone_android, 'جميع البيانات محفوظة داخل الجهاز محليًا'),
                            ),
                            SizedBox(
                              width: 260,
                              child: _featureItem(Icons.backup_outlined, 'إنشاء نسخة احتياطية JSON بعد كل تعديل'),
                            ),
                            SizedBox(
                              width: 260,
                              child: _featureItem(Icons.email_outlined, 'مشاركة ملف النسخة إلى بريدك الشخصي من داخل التطبيق'),
                            ),
                          ],
                        ),
                        const SizedBox(height: 18),
                        TextFormField(
                          controller: _emailController,
                          keyboardType: TextInputType.emailAddress,
                          decoration: const InputDecoration(
                            labelText: 'البريد الشخصي',
                            prefixIcon: Icon(Icons.email_outlined),
                          ),
                          validator: (value) {
                            final text = value?.trim() ?? '';
                            if (text.isEmpty) return 'أدخل البريد الشخصي أولًا';
                            if (!_isValidEmail(text)) return 'أدخل بريدًا صحيحًا';
                            return null;
                          },
                        ),
                        const SizedBox(height: 18),
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
                            label: const Text('حفظ البريد وفتح البرنامج'),
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
  final Future<void> Function(String newEmail) onEmailUpdated;
  final Future<void> Function() onResetProfile;

  const ProfitHomePage({
    super.key,
    required this.userEmail,
    required this.startupMessage,
    required this.onEmailUpdated,
    required this.onResetProfile,
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
  bool _marketLoading = true;
  bool _marketRefreshing = false;
  int _selectedIndex = 0;
  DateTime _selectedDate = DateTime.now();
  DateTime _reportDate = DateTime.now();
  ProfitEntry? _editingEntry;
  Timer? _marketTimer;
  String _userEmail = '';
  String? _marketError;
  MarketSnapshot? _marketSnapshot;
  BackupSnapshotInfo _backupInfo = const BackupSnapshotInfo(exists: false);

  @override
  void initState() {
    super.initState();
    _userEmail = widget.userEmail;
    _searchController.addListener(_handleSearchChanged);
    _loadEntries(showStartupMessage: true);
    unawaited(_refreshMarketData(initialLoad: true));
    _marketTimer = Timer.periodic(
      const Duration(minutes: 5),
      (_) => unawaited(_refreshMarketData()),
    );
  }

  @override
  void dispose() {
    _salesController.dispose();
    _costController.dispose();
    _expensesController.dispose();
    _notesController.dispose();
    _marketTimer?.cancel();
    _searchController
      ..removeListener(_handleSearchChanged)
      ..dispose();
    super.dispose();
  }

  void _handleSearchChanged() {
    if (mounted) setState(() {});
  }


  Future<void> _refreshMarketData({bool initialLoad = false}) async {
    if (_marketRefreshing) return;

    if (mounted) {
      setState(() {
        _marketRefreshing = true;
        if (initialLoad) {
          _marketLoading = true;
        }
      });
    }

    try {
      final snapshot = await MarketService.fetchSnapshot();
      if (!mounted) return;
      setState(() {
        _marketSnapshot = snapshot;
        _marketError = null;
        _marketLoading = false;
        _marketRefreshing = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _marketError = 'تعذّر جلب مؤشرات السوق عبر الإنترنت.';
        _marketLoading = false;
        _marketRefreshing = false;
      });
    }
  }


  Future<void> _loadEntries({bool showStartupMessage = false}) async {
    setState(() => _loading = true);
    try {
      final data = await DatabaseHelper.instance.getAllEntries();
      final backupInfo = await BackupService.instance.readInfo(_userEmail);
      if (!mounted) return;
      setState(() {
        _entries = data;
        _backupInfo = backupInfo;
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
    try {
      final data = await DatabaseHelper.instance.getAllEntries();
      final saved = await BackupService.instance.writeBackup(
        email: _userEmail,
        entries: data,
      );
      final backupInfo = await BackupService.instance.readInfo(_userEmail);
      if (!mounted) return;
      setState(() {
        _backupInfo = backupInfo;
      });
      if (showMessage) {
        if (saved) {
          _showMessage('تم تحديث النسخة الاحتياطية المحلية بنجاح.');
        } else {
          _showMessage(
            'تعذّر تحديث النسخة الاحتياطية المحلية${BackupService.instance.lastError == null ? '' : ': ${BackupService.instance.lastError}'}',
          );
        }
      }
    } catch (e) {
      if (!mounted) return;
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
        subject: 'نسخة احتياطية - برنامج تتبع الأرباح',
        text: 'هذه نسخة احتياطية مرتبطة بالبريد الشخصي: $_userEmail\n\nاختر تطبيق البريد الإلكتروني وأرسل الملف إلى بريدك الشخصي للاحتفاظ بنسخة خارج الجهاز.',
      );
    } catch (e) {
      if (!mounted) return;
      _showMessage('تعذّرت مشاركة النسخة الاحتياطية: $e');
    } finally {
      if (mounted) setState(() => _sharingBackup = false);
    }
  }

  Future<void> _changeEmail() async {
    final controller = TextEditingController(text: _userEmail);
    final formKey = GlobalKey<FormState>();

    final newEmail = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('تغيير البريد الشخصي'),
        content: Form(
          key: formKey,
          child: TextFormField(
            controller: controller,
            keyboardType: TextInputType.emailAddress,
            decoration: const InputDecoration(
              labelText: 'البريد الشخصي الجديد',
              prefixIcon: Icon(Icons.alternate_email),
            ),
            validator: (value) {
              final text = value?.trim() ?? '';
              final emailRegExp = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
              if (text.isEmpty) return 'أدخل البريد الجديد';
              if (!emailRegExp.hasMatch(text)) return 'أدخل بريدًا صحيحًا';
              return null;
            },
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () {
              if (!formKey.currentState!.validate()) return;
              Navigator.pop(context, controller.text.trim());
            },
            child: const Text('حفظ'),
          ),
        ],
      ),
    );

    controller.dispose();

    if (newEmail == null) return;
    final normalized = newEmail.trim().toLowerCase();
    final currentEntries = await DatabaseHelper.instance.getAllEntries();
    await widget.onEmailUpdated(normalized);
    await BackupService.instance.writeBackup(email: normalized, entries: currentEntries);

    if (!mounted) return;
    setState(() {
      _userEmail = normalized;
    });
    await _loadEntries();
    _showMessage('تم تحديث البريد وربط النسخة الاحتياطية به.');
  }

  Future<void> _resetInitialSetup() async {
    final approved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('إعادة الإعداد الأولي'),
        content: const Text(
          'سيتم حذف البريد الشخصي المحفوظ فقط، وستبقى بياناتك داخل الجهاز وملف النسخة الاحتياطية المحلي كما هي. هل تريد المتابعة؟',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('متابعة'),
          ),
        ],
      ),
    );

    if (approved != true) return;
    await widget.onResetProfile();
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

  String _compactDateTimeText(DateTime value) {
    return DateFormat('dd/MM – HH:mm', 'ar').format(value);
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
            ? 'تم تحديث السجل وتحديث النسخة المحلية.'
            : 'تم حفظ السجل وتحديث النسخة المحلية.',
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
      _showMessage('تم حذف السجل وتحديث النسخة المحلية.');
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
              Text('الربح قبل المصاريف: ${_currency(entry.grossProfit)}'),
              Text(
                'صافي الربح: ${_currency(entry.netProfit)}',
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

  Widget _marketTile(MarketQuote quote) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 21,
                  backgroundColor: quote.color.withValues(alpha: 0.12),
                  child: Icon(quote.icon, color: quote.color),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        quote.title,
                        style: const TextStyle(fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        quote.symbol,
                        style: const TextStyle(fontSize: 12.5, color: Colors.black54),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Text(
              _marketValue(quote),
              style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 6),
            Text(
              quote.unit,
              style: const TextStyle(color: Colors.black54),
            ),
            const SizedBox(height: 10),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              decoration: BoxDecoration(
                color: quote.color.withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(14),
              ),
              child: Text(
                quote.sourceUpdatedAt == null
                    ? 'المصدر: ${quote.sourceLabel}'
                    : 'المصدر: ${quote.sourceLabel} • ${_compactDateTimeText(quote.sourceUpdatedAt!)}',
                style: TextStyle(
                  fontSize: 12.5,
                  color: quote.color,
                  fontWeight: FontWeight.w600,
                ),
              ),
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
          padding: EdgeInsets.all(22),
          child: Row(
            children: [
              SizedBox(
                width: 22,
                height: 22,
                child: CircularProgressIndicator(strokeWidth: 2.5),
              ),
              SizedBox(width: 12),
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

    return Column(
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                _marketError == null
                    ? 'آخر مزامنة: ${_dateTimeText(snapshot.fetchedAt)}'
                    : 'يعرض آخر بيانات ناجحة • ${_dateTimeText(snapshot.fetchedAt)}',
                style: const TextStyle(color: Colors.black54),
              ),
            ),
            OutlinedButton.icon(
              onPressed: _marketRefreshing ? null : _refreshMarketData,
              icon: _marketRefreshing
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.wifi_tethering_outlined),
              label: Text(_marketRefreshing ? 'جارٍ التحديث' : 'تحديث مباشر'),
            ),
          ],
        ),
        const SizedBox(height: 12),
        _statsWrap(snapshot.quotes.map(_marketTile).toList(growable: false)),
      ],
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
                      label: 'نسخة احتياطية',
                      onPressed: () => _writeBackup(showMessage: true),
                    ),
                    _outlineActionButton(
                      icon: Icons.forward_to_inbox_outlined,
                      label: 'إرسال إلى البريد',
                      onPressed: _sharingBackup ? null : _shareBackupToEmail,
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
            title: 'إجمالي المصاريف',
            value: _currency(_expensesTotal(_entries)),
            icon: Icons.payments_outlined,
            color: Colors.orange,
            footer: 'إجمالي السجلات: ${_entries.length}',
          ),
        ]),
        const SizedBox(height: 20),
        _sectionHeader(
          'مؤشرات السوق المتصلة بالإنترنت',
          subtitle: 'سعر الذهب أونصة، سعر الفضة أونصة، وسعر اليورو مقابل الدولار من مصادر خارجية يتم تحديثها تلقائيًا.',
        ),
        const SizedBox(height: 10),
        _buildMarketSection(),
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
                      labelText: 'مبلغ البيع اليومي',
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
                      labelText: 'مبلغ الكوست اليومي',
                      prefixIcon: Icon(Icons.inventory_2_outlined),
                    ),
                    onChanged: (_) => setState(() {}),
                    validator: (value) {
                      final number = _parseNumber(value ?? '');
                      if (number < 0) return 'أدخل كوست صحيح';
                      return null;
                    },
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _expensesController,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
                    decoration: const InputDecoration(
                      labelText: 'المصاريف اليومية',
                      prefixIcon: Icon(Icons.money_off_csred_outlined),
                    ),
                    onChanged: (_) => setState(() {}),
                    validator: (value) {
                      final number = _parseNumber(value ?? '');
                      if (number < 0) return 'أدخل مصاريف صحيحة';
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
                      title: 'الربح قبل المصاريف',
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
            footer: 'الربح قبل المصاريف: ${_currency(_grossTotal(monthlyEntries))}',
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
                _infoRow('البريد الشخصي', _userEmail),
                _infoRow(
                  'النسخة المحلية',
                  _backupInfo.exists
                      ? 'متوفرة (${_backupInfo.entriesCount} سجل)'
                      : 'غير موجودة بعد',
                  valueColor: _backupInfo.exists ? Colors.green.shade700 : Colors.orange.shade900,
                ),
                _infoRow(
                  'آخر تحديث',
                  _backupInfo.updatedAt == null ? '—' : _dateTimeText(_backupInfo.updatedAt!),
                ),
                _infoRow(
                  'مسار الملف',
                  _backupInfo.filePath ?? 'سيظهر بعد إنشاء أول نسخة احتياطية',
                ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: [
                    FilledButton.icon(
                      onPressed: () => _writeBackup(showMessage: true),
                      icon: const Icon(Icons.backup_outlined),
                      label: const Text('نسخة احتياطية الآن'),
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
                      label: const Text('مشاركة النسخة إلى البريد'),
                    ),
                    OutlinedButton.icon(
                      onPressed: _changeEmail,
                      icon: const Icon(Icons.edit_outlined),
                      label: const Text('تغيير البريد'),
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
                _launchCheckItem('يوجد ملف نسخة احتياطية محلي مرتبط بالبريد الشخصي.', _backupInfo.exists),
                _launchCheckItem('يمكن إرسال النسخة الاحتياطية إلى البريد الشخصي عبر تطبيق البريد في الجهاز.', true),
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
                  'حفظ البيانات داخل الجهاز يعمل تلقائيًا. أما الحفظ داخل البريد الشخصي فلا يتم بصمت من دون خدمة بريد خارجية؛ لذلك أضفنا زر مشاركة مباشر يجهّز ملف النسخة الاحتياطية ويرسله إلى تطبيق البريد على هاتفك.',
                  style: TextStyle(height: 1.6),
                ),
                const SizedBox(height: 14),
                OutlinedButton.icon(
                  onPressed: _resetInitialSetup,
                  icon: const Icon(Icons.restart_alt),
                  label: const Text('إعادة الإعداد الأولي'),
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
        title: const Text('برنامج تتبع الأرباح'),
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
