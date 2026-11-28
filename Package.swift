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
            url: "https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/apple-engine-ca2421677d81b1d9/EmbedPDFBindings.xcframework.zip",
            checksum: "0ef9812708db073f497ee910685875306ac57c721dfbe398a11fd6b575932d15"
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
