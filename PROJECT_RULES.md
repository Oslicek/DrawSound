# Project Rules

## Software Engineering Principles

### Test-Driven Development (TDD)
1. **Red**: Write a failing test first
2. **Green**: Write the minimum code to make the test pass
3. **Refactor**: Clean up the code while keeping tests green
4. **Commit**: Commit and push the code to 

### SOLID Principles
- **S**ingle Responsibility: Each class should have one reason to change
- **O**pen/Closed: Open for extension, closed for modification
- **L**iskov Substitution: Derived classes must be substitutable for base classes
- **I**nterface Segregation: Many specific interfaces over one general-purpose interface
- **D**ependency Inversion: Depend on abstractions, not concretions

### General Best Practices
- Keep methods small and focused (< 20 lines ideal)
- Meaningful naming over comments
- DRY (Don't Repeat Yourself) - but don't over-abstract prematurely
- YAGNI (You Aren't Gonna Need It) - implement only what's needed now
- Fail fast - validate inputs early, throw meaningful exceptions
- Prefer composition over inheritance

---

## C# Conventions

### Naming
- `PascalCase`: Classes, methods, properties, public fields, constants
- `camelCase`: Local variables, parameters
- `_camelCase`: Private fields (with underscore prefix)
- `IPascalCase`: Interfaces (with I prefix)
- `TPascalCase`: Generic type parameters (with T prefix)

### Code Style
- Use `var` when type is obvious from right-hand side
- Prefer expression-bodied members for single-line methods/properties
- Use `nameof()` instead of hardcoded strings for member names
- Prefer `string.IsNullOrEmpty()` or `string.IsNullOrWhiteSpace()`
- Use nullable reference types (`?`) and handle nullability explicitly
- Prefer pattern matching (`is`, `switch` expressions)

### Async/Await
- Suffix async methods with `Async`
- Use `ConfigureAwait(false)` in library code
- Avoid `async void` except for event handlers
- Prefer `ValueTask` for hot paths that often complete synchronously

### Error Handling
- Use specific exception types
- Don't catch `Exception` unless re-throwing
- Use `when` clause for conditional catches
- Prefer result types over exceptions for expected failures

---

## .NET Best Practices

### Dependency Injection
- Register services in `MauiProgram.cs`
- Use constructor injection
- Prefer `IServiceCollection` extension methods for registration
- Scoped services for per-request/per-page, Singleton for shared state

### Configuration
- Use `appsettings.json` for configuration
- Use Options pattern (`IOptions<T>`) for typed configuration
- Keep secrets out of source control

### Performance
- Use `Span<T>` and `Memory<T>` for buffer operations
- Pool objects and arrays when appropriate (`ArrayPool<T>`)
- Use `StringBuilder` for string concatenation in loops
- Profile before optimizing

---

## .NET MAUI Guidelines

### Architecture
- Use **MVVM** (Model-View-ViewModel) pattern
- ViewModels should not reference Views
- Use `CommunityToolkit.Mvvm` for MVVM implementation
- Keep Views (XAML) focused on UI, logic in ViewModels

### XAML
- Use `x:DataType` for compiled bindings (performance)
- Prefer `StaticResource` over `DynamicResource` when value won't change
- Define reusable styles in `Resources/Styles/`
- Use `ContentView` for reusable UI components

### Navigation
- Use Shell navigation for simple apps
- Register routes in `AppShell.xaml`
- Pass parameters via query strings or navigation parameters

### Platform-Specific Code
- Use `#if ANDROID`, `#if IOS` etc. sparingly
- Prefer dependency injection with platform implementations
- Use `Platforms/` folder for platform-specific code
- Use handlers for custom platform rendering

### Performance
- Use `CollectionView` over `ListView`
- Implement virtualization for large lists
- Avoid layout nesting (keep visual tree shallow)
- Use `x:DataType` for compiled bindings
- Cache images with `ImageSource` caching

### Resources
- Place images in `Resources/Images/`
- Use SVG when possible for scalability
- Place fonts in `Resources/Fonts/`
- Use `MauiIcon` for icons

---

## Testing

### Unit Tests
- One assertion per test (when practical)
- Use AAA pattern: Arrange, Act, Assert
- Name tests: `MethodName_Scenario_ExpectedResult`
- Mock external dependencies
- Test edge cases and error conditions

### UI Tests
- Use `Microsoft.Maui.Testing` or Appium
- Test critical user flows
- Keep UI tests focused and fast

### Test Project Structure
```
DrawSound.Tests/
├── Unit/
│   ├── ViewModels/
│   ├── Services/
│   └── Models/
└── Integration/
```

---

## Git Workflow

- Write meaningful commit messages
- Commit small, focused changes
- Keep `main` branch stable
- Use feature branches for new work
- Squash commits before merging when appropriate

---

## Code Review Checklist
- [ ] Tests included and passing
- [ ] No hardcoded values (use constants/config)
- [ ] Error handling appropriate
- [ ] No unused code or imports
- [ ] Naming is clear and consistent
- [ ] No security vulnerabilities
- [ ] Performance considerations addressed

