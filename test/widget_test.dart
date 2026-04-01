import 'package:flutter_test/flutter_test.dart';
import 'package:profit_tracker/main.dart';

void main() {
  testWidgets('app smoke test', (tester) async {
    await tester.pumpWidget(const ProfitTrackerApp());
    await tester.pumpAndSettle();

    expect(find.text('أدخل بريدك الشخصي قبل فتح البرنامج'), findsOneWidget);
  });
}
