import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MapView, type Layers } from './MapView'
import { rivertown } from '../test/fixture'

const ALL: Layers = { terrain: true, edges: true, minors: true, tokens: true }

describe('MapView', () => {
  it('renders token labels for promoted features', () => {
    render(<MapView model={rivertown} selected={null} onSelect={() => {}} layers={ALL} approximateTerrain />)
    expect(screen.getByText('[01]')).toBeInTheDocument()
    expect(screen.getByText('[02]')).toBeInTheDocument()
  })

  it('selects a feature on click', () => {
    const onSelect = vi.fn()
    render(<MapView model={rivertown} selected={null} onSelect={onSelect} layers={ALL} approximateTerrain />)
    fireEvent.click(screen.getByText('[01]'))
    expect(onSelect).toHaveBeenCalledWith('[01]')
  })

  it('hides token labels when the tokens layer is off', () => {
    render(<MapView model={rivertown} selected={null} onSelect={() => {}} layers={{ ...ALL, tokens: false }} approximateTerrain />)
    expect(screen.queryByText('[01]')).not.toBeInTheDocument()
  })
})
