import { useState } from 'react'
import { QueryKrafter } from './QueryKrafter.jsx'

const config = JSON.stringify({
  allowNot: true,
  allowInvert: true,
  allowSorting: true,
  filters: [
    { id: 'customer', label: 'Customer', type: 'string', group: 'Order' },
    { id: 'total', label: 'Total', type: 'double', group: 'Order', validation: { min: '0' } },
    { id: 'orderDate', label: 'Order date', type: 'date', group: 'Order' },
    { id: 'shipped', label: 'Shipped', type: 'boolean', group: 'Order' },
    {
      id: 'status', label: 'Status', type: 'integer', input: 'checkbox', group: 'Order',
      operators: ['in', 'not_in'],
      values: [
        { value: 1, label: 'Pending' },
        { value: 2, label: 'Shipped' },
        { value: 3, label: 'Cancelled' },
      ],
    },
  ],
})

const starter = JSON.stringify({
  condition: 'AND',
  rules: [
    { id: 'total', operator: 'greater', value: 10 },
    { id: 'status', operator: 'in', value: [1, 2], data: { show: false } },
  ],
})

export default function App() {
  const [designMode, setDesignMode] = useState(true)
  const [detail, setDetail] = useState(null)

  return (
    <>
      <header>
        <strong className="brand">
          <img src="/img/Logo.png" alt="QueryKrafter" />
          <span>React demo</span>
        </strong>
        <label>
          <input type="checkbox" checked={designMode} onChange={event => setDesignMode(event.target.checked)} />
          Design mode
        </label>
      </header>
      <main>
        <QueryKrafter
          config={config}
          rules={starter}
          designMode={designMode}
          sqlDialect="sqlite"
          parameterStyle="inline"
          onChanged={setDetail}
        />
        <section>
          <h2>SQL preview</h2>
          <pre>{detail?.isValid ? (detail.sql?.text || '(no filter)') : detail?.errors?.map(error => error.message).join('\n') || '-'}</pre>
        </section>
        <section>
          <h2>Rules</h2>
          <pre>{detail ? JSON.stringify(detail.rules, null, 2) : '-'}</pre>
        </section>
      </main>
    </>
  )
}
