import { useEffect, useRef } from 'react'

/**
 * React wrapper for <qk-query-builder>. index.html loads the runtime that defines the element.
 * onChanged receives { rules, isValid, errors, sql }.
 */
export function QueryKrafter({
  config,
  rules,
  designMode = false,
  readOnly = false,
  theme = 'light',
  language,
  sqlDialect,
  parameterStyle,
  showErrors = true,
  onChanged,
}) {
  const ref = useRef(null)
  const handler = useRef(onChanged)

  useEffect(() => {
    handler.current = onChanged
  })

  useEffect(() => {
    const element = ref.current
    const changed = event => handler.current?.(event.detail)
    element.addEventListener('qk-changed', changed)
    return () => element.removeEventListener('qk-changed', changed)
  }, [])

  return (
    <qk-query-builder
      ref={ref}
      config={config}
      rules={rules}
      design-mode={String(designMode)}
      read-only={String(readOnly)}
      theme={theme}
      language={language}
      sql-dialect={sqlDialect}
      parameter-style={parameterStyle}
      show-errors={String(showErrors)}
    />
  )
}
