// swift-tools-version: 6.2

import PackageDescription

let package = Package(
    name: "EmbedPDF",
    platforms: [.iOS(.v17), .macOS(.v14)],
    products: [
        .library(name: "EmbedPDF", targets: ["EmbedPDF"]),
        .library(name: "EmbedPDFModels", targets: ["EmbedPDFModels"]),
        .library(name: "EmbedPDFUI", targets: ["EmbedPDFUI"]),
    ],
    targets: [
        .binaryTarget(
            name: "EmbedPDFBindings",
            url: "https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/apple-engine-44a23eb0573339c7/EmbedPDFBindings.xcframework.zip",
            checksum: "e2e7a1ff7f04f087f9739acc88d43d39b90d083462c34f94058a69d87d1b15b3"
        ),
        .target(
            name: "EmbedPDF",
            dependencies: ["EmbedPDFBindings"],
            path: "platforms/apple/EmbedPDF/Sources/EmbedPDF",
            linkerSettings: [
                .linkedLibrary("c++"),
                .linkedFramework("CoreFoundation"),
                .linkedFramework("CoreGraphics"),
                .linkedFramework("Foundation"),
                .linkedFramework("AppKit", .when(platforms: [.macOS])),
            ]
        ),
        .target(
            name: "EmbedPDFModels",
            dependencies: ["EmbedPDF"],
            path: "platforms/apple/EmbedPDF/Sources/EmbedPDFModels"
        ),
        .target(
            name: "EmbedPDFUI",
            dependencies: ["EmbedPDF", "EmbedPDFModels"],
            path: "platforms/apple/EmbedPDF/Sources/EmbedPDFUI"
        ),
    ]
)
