# NeptuneLiteUser carga com.pax.dal.impl.DALProxy por reflexion: R8 no debe recortar el SDK.
# Mismas reglas que jniLibs/NeptuneProguardRules.pro del SDK 4.15.00.
-keep class com.pax.dal.** { <fields>; <methods>; }
-keep class com.pax.nep.** { <fields>; <methods>; }
-keep class com.pax.neptunelite.** { <fields>; <methods>; }
