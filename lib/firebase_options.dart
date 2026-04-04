import 'package:firebase_core/firebase_core.dart' show FirebaseOptions;
import 'package:flutter/foundation.dart' show TargetPlatform, defaultTargetPlatform, kIsWeb;

class DefaultFirebaseOptions {
  static FirebaseOptions get currentPlatform {
    if (kIsWeb) {
      throw UnsupportedError(
        'لم يتم إعداد Firebase للويب في هذا المشروع. استخدم Android أو أضف إعدادات الويب أولًا.',
      );
    }

    switch (defaultTargetPlatform) {
      case TargetPlatform.android:
        return android;
      case TargetPlatform.iOS:
      case TargetPlatform.macOS:
      case TargetPlatform.windows:
      case TargetPlatform.linux:
        throw UnsupportedError(
          'ملف firebase_options.dart الحالي مجهز لأندرويد فقط. أضف إعدادات المنصة الحالية عند الحاجة.',
        );
      case TargetPlatform.fuchsia:
        throw UnsupportedError('Firebase غير مدعوم على Fuchsia في هذا المشروع.');
    }
  }

  static const FirebaseOptions android = FirebaseOptions(
    apiKey: 'AIzaSyCA0IAkOuIo3CAobgtWArgnMdVqd0WsaPY',
    appId: '1:565086596003:android:17e175ce8105c2c4664816',
    messagingSenderId: '565086596003',
    projectId: 'maen-accountings',
    storageBucket: 'maen-accountings.firebasestorage.app',
  );
}
