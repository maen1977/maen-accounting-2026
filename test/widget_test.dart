import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:profit_tracker/main.dart';

void main() {
  testWidgets('splash screen renders', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: SplashLoadingScreen()));

    expect(find.text('جارٍ تجهيز Maen Accountings'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });
}
